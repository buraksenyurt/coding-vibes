using System.Security.Cryptography;
using DockerCity.Data;
using DockerCity.Data.Entities;
using DockerCity.Data.Preferences;
using DockerCity.Data.Stores;
using DockerCity.Domain;
using DockerCity.Domain.Catalog;
using DockerCity.Domain.Layout;
using DockerCity.Parsing.Compose;
using DockerCity.Parsing.Mapping;

namespace DockerCity.App.Services;

public sealed record LoadedCity(
    CityMap Map,
    CityLayout Layout,
    IImageCatalog Catalog,
    int ProjectId,
    string Hash,
    bool ChangedSinceLastVisit);

// Holds the pieces that outlive a single file: the database connection, the
// catalog read from it, and the builder wired to that catalog.
//
// Every database call goes through one gate. A DbContext is not thread-safe
// and UI events overlap more than one expects: a preference toggled while a
// file is loading, or a drop saved while a reload is still running, would
// otherwise throw "a second operation was started on this context".
public sealed class CityWorkspace : IDisposable
{
    private readonly ComposeFileReader _reader = new();
    private readonly ICityLayoutEngine _layoutEngine = new GridCityLayoutEngine();
    private readonly SemaphoreSlim _gate = new(1, 1);

    private DockerCityDbContext? _context;
    private IImageCatalog? _catalog;
    private CityMapBuilder? _builder;
    private ILayoutStore? _layouts;
    private IComposeProjectStore? _projects;
    private AppPreferences? _preferences;

    public int MappingCount => _catalog?.All.Count ?? 0;

    public bool IsInitialised => _builder is not null && _preferences is not null;

    public string DatabaseFile => DockerCityPaths.DatabaseFile;

    public async Task InitialiseAsync(CancellationToken cancellationToken = default)
    {
        _context = await DatabaseInitializer.OpenAsync(cancellationToken);
        _catalog = await SqliteImageCatalog.LoadAsync(_context, cancellationToken);

        _builder = new CityMapBuilder(
            new ServiceFactory(new CatalogServiceCategoryResolver(_catalog)));

        _layouts = new LayoutStore(_context);
        _projects = new ComposeProjectStore(_context);
        _preferences = new AppPreferences(new AppSettingsStore(_context));
    }

    public async Task<LoadedCity> LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        var (builder, catalog, layouts, projects) = Require();

        // Parsing and mapping are the only CPU-bound part; keep them off the
        // UI thread. The database work below is already asynchronous.
        var file = await Task.Run(() => _reader.ReadFromFile(path), cancellationToken);
        var map = builder.Build(file);
        var layout = _layoutEngine.Arrange(map);
        var hash = await HashFileAsync(path, cancellationToken);

        return await GatedAsync(async () =>
        {
            // Read the previous visit before OpenAsync overwrites it.
            var previous = await projects.FindAsync(path, cancellationToken);
            var changed = previous?.FileHash is { } oldHash && oldHash != hash;

            var project = await projects.OpenAsync(path, hash, cancellationToken);
            var stored = await layouts.LoadAsync(project.Id, cancellationToken);

            if (stored.Count > 0)
            {
                // A position the user chose beats the computed one. Districts
                // are recalculated so their borders follow the figures.
                var positions = layout.Nodes.ToDictionary(
                    pair => pair.Key,
                    pair => stored.TryGetValue(pair.Key, out var saved)
                        ? new LayoutPoint(saved.X, saved.Y)
                        : pair.Value,
                    StringComparer.Ordinal);

                layout = _layoutEngine.Rebound(map, positions);
            }

            return new LoadedCity(map, layout, catalog, project.Id, hash, changed);
        }, cancellationToken);
    }

    public CityLayout Rebound(CityMap map, IReadOnlyDictionary<string, LayoutPoint> nodes) =>
        _layoutEngine.Rebound(map, nodes);

    public Task SaveLayoutAsync(
        int projectId,
        string serviceName,
        double x,
        double y,
        CancellationToken cancellationToken = default)
    {
        var (_, _, layouts, _) = Require();

        return GatedAsync(
            () => layouts.SaveAsync(projectId, serviceName, new ServicePosition(x, y, IsPinned: true), cancellationToken),
            cancellationToken);
    }

    public Task<int> ClearLayoutAsync(int projectId, CancellationToken cancellationToken = default)
    {
        var (_, _, layouts, _) = Require();

        return GatedAsync(() => layouts.ClearAsync(projectId, cancellationToken), cancellationToken);
    }

    public Task<IReadOnlyList<ComposeProjectEntity>> RecentAsync(CancellationToken cancellationToken = default)
    {
        var (_, _, _, projects) = Require();

        return GatedAsync(() => projects.RecentAsync(10, cancellationToken), cancellationToken);
    }

    public Task<int> ClearRecentAsync(CancellationToken cancellationToken = default)
    {
        var (_, _, _, projects) = Require();

        return GatedAsync(() => projects.ClearRecentAsync(cancellationToken), cancellationToken);
    }

    public Task<int> RemoveFromRecentAsync(string path, CancellationToken cancellationToken = default)
    {
        var (_, _, _, projects) = Require();

        return GatedAsync(() => projects.RemoveFromRecentAsync(path, cancellationToken), cancellationToken);
    }

    public Task<T> UsePreferencesAsync<T>(
        Func<AppPreferences, Task<T>> action,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        var preferences = _preferences ?? throw new InvalidOperationException("The workspace has not been initialised.");

        return GatedAsync(() => action(preferences), cancellationToken);
    }

    public Task UsePreferencesAsync(
        Func<AppPreferences, Task> action,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);

        return UsePreferencesAsync(
            async preferences =>
            {
                await action(preferences);
                return true;
            },
            cancellationToken);
    }

    // Lets the app notice that a file changed, both since the last visit and
    // while it is open.
    public static async Task<string> HashFileAsync(string path, CancellationToken cancellationToken = default)
    {
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);

        return Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken));
    }

    private async Task<T> GatedAsync<T>(Func<Task<T>> work, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);

        try
        {
            return await work();
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task GatedAsync(Func<Task> work, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);

        try
        {
            await work();
        }
        finally
        {
            _gate.Release();
        }
    }

    private (CityMapBuilder Builder, IImageCatalog Catalog, ILayoutStore Layouts, IComposeProjectStore Projects) Require()
    {
        if (_builder is null || _catalog is null || _layouts is null || _projects is null)
        {
            throw new InvalidOperationException("The workspace has not been initialised.");
        }

        return (_builder, _catalog, _layouts, _projects);
    }

    public void Dispose()
    {
        _context?.Dispose();
        _gate.Dispose();
    }
}
