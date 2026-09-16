using DockerCity.Domain.Values;

namespace DockerCity.Domain.Catalog;

// Adapts any catalog to the seam ServiceFactory depends on. Swapping the
// in-memory rules for database-backed ones needs no change to the factory.
public class CatalogServiceCategoryResolver : IServiceCategoryResolver
{
    private readonly IImageCatalog _catalog;

    public CatalogServiceCategoryResolver(IImageCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        _catalog = catalog;
    }

    public ServiceCategory Resolve(ImageRef image) =>
        _catalog.Match(image)?.Category ?? ServiceCategory.Unknown;
}
