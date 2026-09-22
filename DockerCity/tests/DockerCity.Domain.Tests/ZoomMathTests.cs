using DockerCity.Domain.Layout;

namespace DockerCity.Domain.Tests;

public class ZoomMathTests
{
    [Theory]
    [InlineData(1.0, 1.1)]
    [InlineData(1.05, 1.1)]
    [InlineData(0.25, 0.33)]
    [InlineData(3.0, 3.0)]
    public void Zooming_in_lands_on_the_next_stop(double current, double expected)
    {
        Assert.Equal(expected, ZoomMath.StepIn(current));
    }

    [Theory]
    [InlineData(1.0, 0.9)]
    [InlineData(0.8, 0.75)]
    [InlineData(0.25, 0.25)]
    public void Zooming_out_lands_on_the_previous_stop(double current, double expected)
    {
        Assert.Equal(expected, ZoomMath.StepOut(current));
    }

    [Fact]
    public void Stepping_in_then_out_returns_to_a_stop()
    {
        Assert.Equal(1.0, ZoomMath.StepOut(ZoomMath.StepIn(1.0)));
    }

    [Theory]
    [InlineData(0.1, ZoomMath.Minimum)]
    [InlineData(10, ZoomMath.Maximum)]
    [InlineData(1.5, 1.5)]
    public void Zoom_is_clamped(double requested, double expected)
    {
        Assert.Equal(expected, ZoomMath.Clamp(requested));
    }

    [Fact]
    public void Fit_shrinks_a_large_city()
    {
        var zoom = ZoomMath.Fit(new LayoutBounds(0, 0, 2000, 1000), 1000, 600);

        // Width is the tighter side: (1000 - 48) / 2000.
        Assert.Equal(0.476, zoom, 6);
    }

    [Fact]
    public void Fit_never_enlarges_a_small_city()
    {
        Assert.Equal(1, ZoomMath.Fit(new LayoutBounds(0, 0, 400, 300), 1000, 600));
    }

    [Fact]
    public void Fit_never_goes_below_the_minimum()
    {
        Assert.Equal(ZoomMath.Minimum, ZoomMath.Fit(new LayoutBounds(0, 0, 100_000, 100_000), 1000, 600));
    }

    [Fact]
    public void Fit_of_nothing_is_actual_size()
    {
        Assert.Equal(1, ZoomMath.Fit(default, 1000, 600));
    }

    [Fact]
    public void Zooming_keeps_the_point_in_the_middle_in_the_middle()
    {
        // Viewport 800x600 scrolled to (100, 50) at 100%: the middle is (500, 350).
        var (x, y) = ZoomMath.OffsetsKeepingCenter(100, 50, 800, 600, oldZoom: 1, newZoom: 2);

        Assert.Equal(600, x);
        Assert.Equal(400, y);

        // At 200% that world point sits at (1000, 700); minus half the viewport.
        Assert.Equal(500, (x + 400) / 2);
        Assert.Equal(350, (y + 300) / 2);
    }

    [Fact]
    public void Offsets_never_go_negative()
    {
        var (x, y) = ZoomMath.OffsetsToCenterOn(new LayoutPoint(10, 10), 800, 600, 1);

        Assert.Equal(0, x);
        Assert.Equal(0, y);
    }
}
