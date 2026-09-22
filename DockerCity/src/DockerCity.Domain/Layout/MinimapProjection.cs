namespace DockerCity.Domain.Layout;

// Maps between city coordinates and a small overview panel, keeping the
// aspect ratio and centring the city inside the panel.
public sealed class MinimapProjection
{
    public MinimapProjection(LayoutBounds world, double width, double height, double padding = 6)
    {
        var usableWidth = Math.Max(1, width - (padding * 2));
        var usableHeight = Math.Max(1, height - (padding * 2));

        Scale = world.IsEmpty
            ? 1
            : Math.Min(usableWidth / world.Width, usableHeight / world.Height);

        OffsetX = padding + ((usableWidth - (world.Width * Scale)) / 2) - (world.Left * Scale);
        OffsetY = padding + ((usableHeight - (world.Height * Scale)) / 2) - (world.Top * Scale);
    }

    public double Scale { get; }

    public double OffsetX { get; }

    public double OffsetY { get; }

    public LayoutPoint ToMinimap(LayoutPoint world) =>
        new((world.X * Scale) + OffsetX, (world.Y * Scale) + OffsetY);

    public LayoutPoint ToWorld(LayoutPoint minimap) =>
        new((minimap.X - OffsetX) / Scale, (minimap.Y - OffsetY) / Scale);
}
