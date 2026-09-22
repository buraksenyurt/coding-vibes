using DockerCity.Domain.Layout;

namespace DockerCity.Domain.Tests;

public class MinimapProjectionTests
{
    private static readonly LayoutBounds City = new(32, 32, 648, 592);

    [Fact]
    public void The_city_fits_inside_the_panel_with_padding()
    {
        var projection = new MinimapProjection(City, 220, 150, padding: 6);

        var topLeft = projection.ToMinimap(new LayoutPoint(City.Left, City.Top));
        var bottomRight = projection.ToMinimap(new LayoutPoint(City.Right, City.Bottom));

        Assert.InRange(topLeft.X, 6, 214);
        Assert.InRange(bottomRight.X, 6, 214);

        // Height is the tighter side here, so it touches both paddings exactly.
        Assert.Equal(6, topLeft.Y, 9);
        Assert.Equal(144, bottomRight.Y, 9);
    }

    [Fact]
    public void The_aspect_ratio_is_kept()
    {
        var projection = new MinimapProjection(City, 220, 150);

        var topLeft = projection.ToMinimap(new LayoutPoint(City.Left, City.Top));
        var bottomRight = projection.ToMinimap(new LayoutPoint(City.Right, City.Bottom));

        var projectedRatio = (bottomRight.X - topLeft.X) / (bottomRight.Y - topLeft.Y);

        Assert.Equal(City.Width / City.Height, projectedRatio, 9);
    }

    [Fact]
    public void The_narrow_side_is_centred()
    {
        var projection = new MinimapProjection(City, 220, 150, padding: 6);

        var left = projection.ToMinimap(new LayoutPoint(City.Left, City.Top)).X;
        var right = projection.ToMinimap(new LayoutPoint(City.Right, City.Bottom)).X;

        Assert.Equal(left - 0, 220 - right, 9);
    }

    [Theory]
    [InlineData(300, 200)]
    [InlineData(32, 32)]
    [InlineData(648, 592)]
    public void Projection_round_trips(double x, double y)
    {
        var projection = new MinimapProjection(City, 220, 150);

        var back = projection.ToWorld(projection.ToMinimap(new LayoutPoint(x, y)));

        Assert.Equal(x, back.X, 9);
        Assert.Equal(y, back.Y, 9);
    }

    [Fact]
    public void An_empty_city_does_not_divide_by_zero()
    {
        var projection = new MinimapProjection(default, 220, 150);

        Assert.Equal(1, projection.Scale);
        Assert.False(double.IsNaN(projection.OffsetX));
    }
}
