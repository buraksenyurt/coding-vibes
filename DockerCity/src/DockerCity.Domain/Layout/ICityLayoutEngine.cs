namespace DockerCity.Domain.Layout;

public interface ICityLayoutEngine
{
    CityLayout Arrange(CityMap map, LayoutOptions? options = null);
}
