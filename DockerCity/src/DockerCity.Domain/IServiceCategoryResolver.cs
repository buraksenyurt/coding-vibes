using DockerCity.Domain.Values;

namespace DockerCity.Domain;

public interface IServiceCategoryResolver
{
    ServiceCategory Resolve(ImageRef image);
}