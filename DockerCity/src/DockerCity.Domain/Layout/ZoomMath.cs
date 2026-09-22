namespace DockerCity.Domain.Layout;

// Zoom arithmetic kept away from the ScrollViewer, so it can be tested.
// Offsets here are in zoomed pixels, the unit ScrollViewer.ChangeView uses.
public static class ZoomMath
{
    public const double Minimum = 0.25;
    public const double Maximum = 3.0;

    private const double Tolerance = 1e-6;

    // Fixed stops, like a browser: repeated Ctrl+Plus lands on round numbers
    // instead of drifting through 1.1, 1.21, 1.331...
    private static readonly double[] Ladder =
        [0.25, 0.33, 0.5, 0.67, 0.75, 0.9, 1, 1.1, 1.25, 1.5, 1.75, 2, 2.5, 3];

    public static double Clamp(double zoom) => Math.Clamp(zoom, Minimum, Maximum);

    public static double StepIn(double current) =>
        Ladder.FirstOrDefault(stop => stop > current + Tolerance, Maximum);

    public static double StepOut(double current) =>
        Ladder.LastOrDefault(stop => stop < current - Tolerance, Minimum);

    // Shrinks a large city until it fits, but never enlarges a small one:
    // blowing ten figures up to 300% makes them big, not clearer.
    public static double Fit(LayoutBounds content, double viewportWidth, double viewportHeight, double margin = 24)
    {
        if (content.IsEmpty || viewportWidth <= margin * 2 || viewportHeight <= margin * 2)
        {
            return 1;
        }

        var zoom = Math.Min(
            (viewportWidth - (margin * 2)) / content.Width,
            (viewportHeight - (margin * 2)) / content.Height);

        return Math.Clamp(zoom, Minimum, 1);
    }

    // After a zoom from the menu or keyboard, the point in the middle of the
    // window should still be in the middle.
    public static (double X, double Y) OffsetsKeepingCenter(
        double offsetX,
        double offsetY,
        double viewportWidth,
        double viewportHeight,
        double oldZoom,
        double newZoom)
    {
        var centerX = (offsetX + (viewportWidth / 2)) / oldZoom;
        var centerY = (offsetY + (viewportHeight / 2)) / oldZoom;

        return OffsetsToCenterOn(new LayoutPoint(centerX, centerY), viewportWidth, viewportHeight, newZoom);
    }

    public static (double X, double Y) OffsetsToCenterOn(
        LayoutPoint world,
        double viewportWidth,
        double viewportHeight,
        double zoom) =>
        (Math.Max(0, (world.X * zoom) - (viewportWidth / 2)),
         Math.Max(0, (world.Y * zoom) - (viewportHeight / 2)));
}
