using DockerCity.Domain.Catalog;

namespace DockerCity.Domain;

// Convenience over the built-in catalog, kept so callers that do not care
// where the rules live can stay a single new-expression.
public sealed class InMemoryServiceCategoryResolver()
    : CatalogServiceCategoryResolver(new InMemoryImageCatalog());
