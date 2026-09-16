using DockerCity.Domain.Values;

namespace DockerCity.Domain.Catalog;

// Where the rules come from is the only thing implementations decide.
// How a winner is picked stays here so every source behaves the same.
public abstract class ImageCatalog : IImageCatalog
{
    public abstract IReadOnlyList<ImageMapping> All { get; }

    public ImageMapping? Match(ImageRef image) =>
        All.Where(mapping => mapping.Matches(image))
           .OrderByDescending(mapping => mapping.Priority)
           .ThenByDescending(mapping => mapping.Pattern.Length)
           .FirstOrDefault();
}
