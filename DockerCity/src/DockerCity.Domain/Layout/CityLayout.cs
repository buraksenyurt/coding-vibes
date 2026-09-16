namespace DockerCity.Domain.Layout;

public sealed record DistrictBounds(
    string DistrictName,
    bool IsImplicit,
    double X,
    double Y,
    double Width,
    double Height);

public sealed record CityLayout(
    IReadOnlyDictionary<string, LayoutPoint> Nodes,
    IReadOnlyList<DistrictBounds> Districts,
    double Width,
    double Height);
