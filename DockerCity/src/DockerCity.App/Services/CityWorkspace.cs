using System.Security.Cryptography;
using DockerCity.Data;
using DockerCity.Data.Stores;
using DockerCity.Domain;
using DockerCity.Domain.Catalog;
using DockerCity.Domain.Layout;
using DockerCity.Parsing.Compose;
using DockerCity.Parsing.Mapping;

namespace DockerCity.App.Services;

public sealed record LoadedCity(CityMap Map, CityLayout Layout, IImageCatalog Catalog, int ProjectId);

// Holds the pieces that outlive a single file: the database connection, the
// catalog read from it, and the builder wired to that catalog.
public sealed class CityWorkspace : IDisposable
{
    private readonly ComposeFileReader _reader = new();
    private readonly ICityLayoutEngine _layoutEngine = new GridCityLayoutEngine();

    private DockerCityDbContext? _context;
    private IImageCatalog? _catalog;
    private CityMapBuilder? _builder;
    private ILayoutStore? _layouts;
    private IComposeProjectStore? _projects;

    public int MappingCount => _catalog?.All.Count ?? 0;

    public async Task InitialiseAsync(CancellationToken cancellationToken = default)
    {
        _context = await DatabaseInitializer.OpenAsync(cancellationToken);
        _catalog = await SqliteImageCatalog.LoadAsync(_context, cancellationToken);

        _builder = new CityMapBuilder(
            new ServiceFactory(new CatalogServiceCategoryResolver(_catalog)));

        _layouts = new LayoutStore(_context);
        _projects = new ComposeProjectStore(_context);
    }

    public async Task<LoadedCity> LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        var (builder, catalog, layouts, projects) = Require();

        // Parsing and mapping are the only CPU-bound part; keep them off the
        // UI thread. The database work below is already asynchronous.
        var file = await Task.Run(() => _reader.ReadFromFile(path), cancellationToken);
        var map = builder.Build(file);
        var layout = _layoutEngine.Arrange(map);

        var project = await projects.OpenAsync(path, await HashAsync(path, cancellationToken), cancellationToken);
        var stored = await layouts.LoadAsync(project.Id, cancellationToken);

        if (stored.Count > 0)
        {
            // A position the user chose beats the computed one. Districts are
            // recalculated so their borders follow the figures.
            var positions = layout.Nodes.ToDictionary(
                pair => pair.Key,
                pair => stored.TryGetValue(pair.Key, out var saved)
                    ? new LayoutPoint(saved.X, saved.Y)
                    : pair.Value,
                StringComparer.Ordinal);

            layout = _layoutEngine.Rebound(map, positions);
        }

        return new LoadedCity(map, layout, catalog, project.Id);
    }

    public CityLayout Rebound(CityMap map, IReadOnlyDictionary<string, LayoutPoint> nodes) =>
        _layoutEngine.Rebound(map, nodes);

    public async Task SaveLayoutAsync(
        int projectId,
        string serviceName,
        double x,
        double y,
        CancellationToken cancellationToken = default)
    {
        var (_, _, layouts, _) = Require();

        await layouts.SaveAsync(
            projectId,
            serviceName,
            new ServicePosition(x, y, IsPinned: true),
            cancellationToken);
    }

    public async Task<int> ClearLayoutAsync(int projectId, CancellationToken cancellationToken = default)
    {
        var (_, _, layouts, _) = Require();

        return await layouts.ClearAsync(projectId, cancellationToken);
    }

    private (CityMapBuilder Builder, IImageCatalog Catalog, ILayoutStore Layouts, IComposeProjectStore Projects) Require()
    {
        if (_builder is null || _catalog is null || _layouts is null || _projects is null)
        {
            throw new InvalidOperationException("The workspace has not been initialised.");
        }

        return (_builder, _catalog, _layouts, _projects);
    }

    // Lets phase 9 notice that a file changed since it was last opened.
    private static async Task<string> HashAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);

        return Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken));
    }

    public void Dispose() => _context?.Dispose();
}
