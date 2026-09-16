using DockerCity.Domain.Values;

namespace DockerCity.Domain.Catalog;

public interface IImageCatalog
{
    IReadOnlyList<ImageMapping> All { get; }

    ImageMapping? Match(ImageRef image);
}
