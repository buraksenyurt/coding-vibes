using DockerCity.Data;
using DockerCity.Domain;
using DockerCity.Domain.Catalog;
using DockerCity.Domain.Layout;
using DockerCity.Parsing.Compose;
using DockerCity.Parsing.Mapping;

namespace DockerCity.App.Services;

public sealed record LoadedCity(CityMap Map, CityLayout Layout, IImageCatalog Catalog);

// Holds the pieces that outlive a single file: the database connection, the
// catalog read from it, and the builder wired to that catalog.
public sealed class CityWorkspace : IDisposable
{
    private readonly ComposeFileReader _reader = new();
    private readonly ICityLayoutEngine _layoutEngine = new GridCityLayoutEngine();

    private DockerCityDbContext? _context;
    private IImageCatalog? _catalog;
    private CityMapBuilder? _builder;

    public int MappingCount => _catalog?.All.Count ?? 0;

    public async Task InitialiseAsync(CancellationToken cancellationToken = default)
    {
        _context = await DatabaseInitializer.OpenAsync(cancellationToken);
        _catalog = await SqliteImageCatalog.LoadAsync(_context, cancellationToken);

        _builder = new CityMapBuilder(
            new ServiceFactory(new CatalogServiceCategoryResolver(_catalog)));
    }

    public LoadedCity Load(string path)
    {
        if (_builder is null || _catalog is null)
        {
            throw new InvalidOperationException("The workspace has not been initialised.");
        }

        var file = _reader.ReadFromFile(path);
        var map = _builder.Build(file);

        return new LoadedCity(map, _layoutEngine.Arrange(map), _catalog);
    }

    public void Dispose() => _context?.Dispose();
}
