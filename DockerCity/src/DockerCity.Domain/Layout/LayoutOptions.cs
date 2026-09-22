namespace DockerCity.Domain.Layout;

public sealed record LayoutOptions
{
    public double NodeWidth { get; init; } = 120;
    public double NodeHeight { get; init; } = 120;
    public double NodeSpacing { get; init; } = 24;

    // Breathing room between a district border and the figures inside it.
    public double DistrictPadding { get; init; } = 32;

    // Vertical gap between two districts.
    public double DistrictSpacing { get; init; } = 48;

    public double Margin { get; init; } = 32;

    public int MaxColumns { get; init; } = 4;

    // Where a figure's icon sits inside its slot. Links attach to a circle
    // around the icon rather than to the whole figure, so arrows point at the
    // service and not at its label.
    public double IconCenterX { get; init; } = 60;
    public double IconCenterY { get; init; } = 41;
    public double IconRadius { get; init; } = 46;

    public static LayoutOptions Default { get; } = new();
}
