using DockerCity.Domain.Layout;

namespace DockerCity.Domain.Tests;

public class LinkRouterTests
{
    private const double Tolerance = 1e-9;

    private static readonly LinkRouter Router = new();

    private static Dictionary<string, LayoutPoint> Nodes(params (string Name, double X, double Y)[] nodes) =>
        nodes.ToDictionary(node => node.Name, node => new LayoutPoint(node.X, node.Y), StringComparer.Ordinal);

    private static double Distance(LayoutPoint a, LayoutPoint b) =>
        Math.Sqrt(((a.X - b.X) * (a.X - b.X)) + ((a.Y - b.Y) * (a.Y - b.Y)));

    private static CityLink DependsOn(string from, string to) => new(CityLinkKind.DependsOn, from, to);

    [Fact]
    public void A_link_to_an_unknown_service_is_not_routed()
    {
        var nodes = Nodes(("a", 0, 0));

        Assert.Null(Router.Route(DependsOn("a", "missing"), nodes));
        Assert.Null(Router.Route(DependsOn("missing", "a"), nodes));
    }

    [Fact]
    public void The_road_starts_and_ends_on_the_circle_around_each_icon()
    {
        var nodes = Nodes(("a", 0, 0), ("b", 300, 200));

        var route = Router.Route(DependsOn("a", "b"), nodes)!.Value;

        var radius = LayoutOptions.Default.IconRadius;
        Assert.Equal(radius, Distance(route.Start, Router.Center(nodes["a"])), Tolerance);
        Assert.Equal(radius, Distance(route.End, Router.Center(nodes["b"])), Tolerance);
    }

    [Fact]
    public void The_arrow_head_sits_behind_the_end_point()
    {
        var route = Router.Route(DependsOn("a", "b"), Nodes(("a", 0, 0), ("b", 300, 0)))!.Value;

        Assert.Equal(LinkRouter.ArrowLength, Distance(route.ArrowLeft, route.End), Tolerance);
        Assert.Equal(LinkRouter.ArrowLength, Distance(route.ArrowRight, route.End), Tolerance);

        // "Behind" means pointing back along the direction the curve arrives in.
        var arrivingX = route.End.X - route.Control2.X;
        var arrivingY = route.End.Y - route.Control2.Y;

        Assert.True(((route.ArrowLeft.X - route.End.X) * arrivingX) + ((route.ArrowLeft.Y - route.End.Y) * arrivingY) < 0);
        Assert.True(((route.ArrowRight.X - route.End.X) * arrivingX) + ((route.ArrowRight.Y - route.End.Y) * arrivingY) < 0);
    }

    [Fact]
    public void A_link_and_its_reverse_bend_to_opposite_sides()
    {
        var nodes = Nodes(("a", 0, 0), ("b", 300, 0));

        var forward = Router.Route(DependsOn("a", "b"), nodes)!.Value;
        var backward = Router.Route(DependsOn("b", "a"), nodes)!.Value;

        var a = Router.Center(nodes["a"]);
        var b = Router.Center(nodes["b"]);

        // Sign of the cross product tells which side of the line a point is on.
        static double Side(LayoutPoint origin, LayoutPoint toward, LayoutPoint point) =>
            ((toward.X - origin.X) * (point.Y - origin.Y)) - ((toward.Y - origin.Y) * (point.X - origin.X));

        Assert.True(Side(a, b, forward.Control1) * Side(a, b, backward.Control1) < 0);
    }

    [Fact]
    public void Icons_that_touch_leave_no_room_for_a_road()
    {
        // Centres 80 apart, while two radii add up to 92.
        Assert.Null(Router.Route(DependsOn("a", "b"), Nodes(("a", 0, 0), ("b", 80, 0))));
    }

    [Fact]
    public void Routes_follow_their_nodes()
    {
        var before = Router.Route(DependsOn("a", "b"), Nodes(("a", 0, 0), ("b", 300, 0)))!.Value;
        var after = Router.Route(DependsOn("a", "b"), Nodes(("a", 0, 0), ("b", 300, 400)))!.Value;

        Assert.NotEqual(before.End, after.End);
        Assert.Equal(before.Start.X > 0, after.Start.X > 0);
    }

    [Fact]
    public void Known_geometry_is_stable()
    {
        // pgadmin -> postgres as the sample file lays them out.
        var route = Router.Route(
            DependsOn("pgadmin", "postgres"),
            Nodes(("pgadmin", 352, 64), ("postgres", 496, 64)))!.Value;

        Assert.Equal(new LayoutPoint(458, 105), route.Start);
        Assert.Equal(new LayoutPoint(510, 105), route.End);
    }
}
