using DockerCity.Domain.Layout;

namespace DockerCity.Domain.Tests;

public class LayoutBoundsTests
{
    [Fact]
    public void Bounds_cover_figures_and_districts()
    {
        var layout = new CityLayout(
            new Dictionary<string, LayoutPoint> { ["a"] = new(100, 100), ["b"] = new(400, 50) },
            [new DistrictBounds("net", false, 60, 20, 500, 250)],
            Width: 2000,
            Height: 2000);

        var bounds = LayoutBounds.Of(layout);

        // The district reaches further left and down; figure b reaches further up.
        Assert.Equal(new LayoutBounds(60, 20, 560, 270), bounds);
    }

    [Fact]
    public void Bounds_ignore_the_empty_canvas_around_the_city()
    {
        var layout = new CityLayout(
            new Dictionary<string, LayoutPoint> { ["a"] = new(0, 0) },
            [],
            Width: 5000,
            Height: 5000);

        var bounds = LayoutBounds.Of(layout);

        Assert.Equal(LayoutOptions.Default.NodeWidth, bounds.Width);
        Assert.Equal(LayoutOptions.Default.NodeHeight, bounds.Height);
    }

    [Fact]
    public void An_empty_layout_has_empty_bounds()
    {
        var bounds = LayoutBounds.Of(new CityLayout(new Dictionary<string, LayoutPoint>(), [], 800, 600));

        Assert.True(bounds.IsEmpty);
    }
}
