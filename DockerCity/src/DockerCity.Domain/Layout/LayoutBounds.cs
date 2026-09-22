namespace DockerCity.Domain.Layout;

// The box that actually holds something: figures and district borders, without
// the empty margin the canvas keeps for itself. Fitting to the window, the
// mini map and cropping all need this rather than the canvas size.
public readonly record struct LayoutBounds(double Left, double Top, double Right, double Bottom)
{
    public double Width => Right - Left;

    public double Height => Bottom - Top;

    public bool IsEmpty => Width <= 0 || Height <= 0;

    public LayoutPoint Center => new((Left + Right) / 2, (Top + Bottom) / 2);

    public static LayoutBounds Of(CityLayout layout, LayoutOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(layout);

        var settings = options ?? LayoutOptions.Default;

        var left = double.MaxValue;
        var top = double.MaxValue;
        var right = double.MinValue;
        var bottom = double.MinValue;

        foreach (var point in layout.Nodes.Values)
        {
            left = Math.Min(left, point.X);
            top = Math.Min(top, point.Y);
            right = Math.Max(right, point.X + settings.NodeWidth);
            bottom = Math.Max(bottom, point.Y + settings.NodeHeight);
        }

        foreach (var district in layout.Districts)
        {
            left = Math.Min(left, district.X);
            top = Math.Min(top, district.Y);
            right = Math.Max(right, district.X + district.Width);
            bottom = Math.Max(bottom, district.Y + district.Height);
        }

        return left > right ? default : new LayoutBounds(left, top, right, bottom);
    }
}
