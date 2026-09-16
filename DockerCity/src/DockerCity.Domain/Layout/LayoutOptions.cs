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

    public static LayoutOptions Default { get; } = new();
}
