namespace DockerCity.Domain.Layout;

public interface ICityLayoutEngine
{
    // First arrangement: decides where every figure goes.
    CityLayout Arrange(CityMap map, LayoutOptions? options = null);

    // Positions are given, only the district regions and the extent are
    // recalculated. Used after the user drags a figure, and after positions
    // are restored from the database.
    CityLayout Rebound(
        CityMap map,
        IReadOnlyDictionary<string, LayoutPoint> nodes,
        LayoutOptions? options = null);
}
