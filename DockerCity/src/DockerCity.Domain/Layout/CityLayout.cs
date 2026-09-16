namespace DockerCity.Domain.Layout;

public sealed record DistrictBounds(
    string DistrictName,
    bool IsImplicit,
    double X,
    double Y,
    double Width,
    double Height)
{
    // True when every member of this district was already placed by an earlier
    // one. The region still has to be drawn, but it sits on top of figures that
    // belong somewhere else, so it is rendered as an outline rather than a fill.
    public bool IsOverlay { get; init; }
}

public sealed record CityLayout(
    IReadOnlyDictionary<string, LayoutPoint> Nodes,
    IReadOnlyList<DistrictBounds> Districts,
    double Width,
    double Height);
