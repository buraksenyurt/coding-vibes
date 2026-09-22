namespace DockerCity.Domain.Layout;

// Turns a CityLink into drawable geometry. Pure arithmetic on purpose: the
// view only copies points into a PathGeometry, so every decision about where
// a road starts, bends and ends can be tested without a window.
public sealed class LinkRouter(LayoutOptions? options = null)
{
    public const double ArrowLength = 11;

    // How far the curve bulges sideways, as a share of its length.
    private const double Curvature = 0.18;

    // Half the opening angle of the arrow head, in radians (about 26 degrees).
    private const double ArrowSpread = 0.45;

    private readonly LayoutOptions _options = options ?? LayoutOptions.Default;

    public LayoutPoint Center(LayoutPoint node) =>
        new(node.X + _options.IconCenterX, node.Y + _options.IconCenterY);

    public LinkPath? Route(CityLink link, IReadOnlyDictionary<string, LayoutPoint> nodes)
    {
        ArgumentNullException.ThrowIfNull(link);
        ArgumentNullException.ThrowIfNull(nodes);

        if (!nodes.TryGetValue(link.From, out var from) || !nodes.TryGetValue(link.To, out var to))
        {
            return null;
        }

        var source = Center(from);
        var target = Center(to);

        var deltaX = target.X - source.X;
        var deltaY = target.Y - source.Y;
        var distance = Math.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
        var radius = _options.IconRadius;

        // Icons touching or overlapping leave no room for a road. Better to
        // draw nothing than a stub pointing the wrong way.
        if (distance <= radius * 2)
        {
            return null;
        }

        var unitX = deltaX / distance;
        var unitY = deltaY / distance;

        var start = new LayoutPoint(source.X + (unitX * radius), source.Y + (unitY * radius));
        var end = new LayoutPoint(target.X - (unitX * radius), target.Y - (unitY * radius));

        // Bend to one side of the direction of travel. A link and its reverse
        // then curve apart instead of being drawn on top of each other.
        var span = distance - (radius * 2);
        var bend = span * Curvature;
        var normalX = -unitY;
        var normalY = unitX;

        var control1 = new LayoutPoint(
            start.X + (unitX * span / 3) + (normalX * bend),
            start.Y + (unitY * span / 3) + (normalY * bend));

        var control2 = new LayoutPoint(
            start.X + (unitX * span * 2 / 3) + (normalX * bend),
            start.Y + (unitY * span * 2 / 3) + (normalY * bend));

        // A cubic Bezier leaves its last control point heading straight for
        // the end, so that direction is the tangent the arrow has to follow.
        var tangentX = end.X - control2.X;
        var tangentY = end.Y - control2.Y;
        var tangentLength = Math.Sqrt((tangentX * tangentX) + (tangentY * tangentY));

        var backX = -tangentX / tangentLength;
        var backY = -tangentY / tangentLength;

        var left = Rotate(backX, backY, ArrowSpread);
        var right = Rotate(backX, backY, -ArrowSpread);

        return new LinkPath(
            start,
            control1,
            control2,
            end,
            new LayoutPoint(end.X + (left.X * ArrowLength), end.Y + (left.Y * ArrowLength)),
            new LayoutPoint(end.X + (right.X * ArrowLength), end.Y + (right.Y * ArrowLength)));
    }

    private static (double X, double Y) Rotate(double x, double y, double angle) =>
        ((x * Math.Cos(angle)) - (y * Math.Sin(angle)),
         (x * Math.Sin(angle)) + (y * Math.Cos(angle)));
}
