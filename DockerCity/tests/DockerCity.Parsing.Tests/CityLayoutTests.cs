using DockerCity.Domain;
using DockerCity.Domain.Layout;
using DockerCity.Parsing.Compose;
using DockerCity.Parsing.Dto;
using DockerCity.Parsing.Mapping;

namespace DockerCity.Parsing.Tests;

// The layout engine lives in Domain but only becomes interesting against a
// real CityMap, so it is exercised from here where the fixture already is.
public class CityLayoutTests
{
    private static CityLayout Sample() => new GridCityLayoutEngine().Arrange(SampleMap());

    private static CityMap BuildFrom(string yaml)
    {
        var file = new ComposeFileReader().ReadFromText(yaml);

        return new CityMapBuilder(new ServiceFactory(new InMemoryServiceCategoryResolver())).Build(file);
    }

    private static CityMap SampleMap()
    {
        ComposeFileDto file = new ComposeFileReader().ReadFromFile(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "docker-compose.yml"));

        return new CityMapBuilder(new ServiceFactory(new InMemoryServiceCategoryResolver())).Build(file);
    }

    [Fact]
    public void Every_service_gets_a_position()
    {
        var layout = Sample();

        Assert.Equal(10, layout.Nodes.Count);
        Assert.All(SampleMap().Services, service => Assert.True(layout.Nodes.ContainsKey(service.Name)));
    }

    [Fact]
    public void No_two_figures_share_a_spot()
    {
        var positions = Sample().Nodes.Values.ToList();

        Assert.Equal(positions.Count, positions.Distinct().Count());
    }

    [Fact]
    public void Explicit_districts_come_before_the_implicit_one()
    {
        var districts = Sample().Districts;

        Assert.Equal(2, districts.Count);
        Assert.Equal("fnp-network", districts[0].DistrictName);
        Assert.False(districts[0].IsImplicit);
        Assert.Equal("default", districts[1].DistrictName);
        Assert.True(districts[1].IsImplicit);
        Assert.True(districts[1].Y > districts[0].Y);
    }

    [Fact]
    public void District_bounds_enclose_their_members()
    {
        var layout = Sample();
        var map = SampleMap();

        foreach (var bounds in layout.Districts)
        {
            var district = map.Districts.Single(candidate => candidate.Name == bounds.DistrictName);

            foreach (var member in district.Members)
            {
                var point = layout.Nodes[member.Name];

                Assert.InRange(point.X, bounds.X, bounds.X + bounds.Width - LayoutOptions.Default.NodeWidth);
                Assert.InRange(point.Y, bounds.Y, bounds.Y + bounds.Height - LayoutOptions.Default.NodeHeight);
            }
        }
    }

    [Fact]
    public void Districts_do_not_overlap_vertically()
    {
        var districts = Sample().Districts;

        for (var index = 1; index < districts.Count; index++)
        {
            var previous = districts[index - 1];
            Assert.True(districts[index].Y >= previous.Y + previous.Height);
        }
    }

    [Fact]
    public void Arrangement_is_repeatable()
    {
        var first = new GridCityLayoutEngine().Arrange(SampleMap());
        var second = new GridCityLayoutEngine().Arrange(SampleMap());

        Assert.Equal(first.Nodes, second.Nodes);
    }

    [Fact]
    public void Known_geometry_is_stable()
    {
        var layout = Sample();

        // Four columns of 120 wide with 24 spacing, inside 32 of padding,
        // inside a 32 margin. Pinned down so a silent change is visible.
        Assert.Equal(new LayoutPoint(64, 64), layout.Nodes["ftp-server"]);
        Assert.Equal(new LayoutPoint(496, 64), layout.Nodes["postgres"]);
        Assert.Equal(new LayoutPoint(64, 208), layout.Nodes["qdrant"]);
        Assert.Equal(new LayoutPoint(64, 440), layout.Nodes["keycloak"]);

        Assert.Equal(680, layout.Width);
        Assert.Equal(624, layout.Height);
    }

    [Fact]
    public void A_district_whose_members_are_all_placed_elsewhere_becomes_an_overlay()
    {
        // "api" belongs to both networks. It can only be drawn once, so the
        // second district has to wrap around wherever the first one put it.
        const string yaml = """
            services:
              api:
                image: nginx
                networks:
                  - back
                  - front
              db:
                image: postgres
                networks:
                  - back
            networks:
              back:
              front:
            """;

        var layout = new GridCityLayoutEngine().Arrange(BuildFrom(yaml));

        var back = layout.Districts.Single(district => district.DistrictName == "back");
        var front = layout.Districts.Single(district => district.DistrictName == "front");

        Assert.False(back.IsOverlay);
        Assert.True(front.IsOverlay);

        // The overlay still encloses its member.
        var api = layout.Nodes["api"];
        Assert.InRange(api.X, front.X, front.X + front.Width - LayoutOptions.Default.NodeWidth);
        Assert.InRange(api.Y, front.Y, front.Y + front.Height - LayoutOptions.Default.NodeHeight);

        // ...and it is narrower than the district that did the placing.
        Assert.True(front.Width < back.Width);
    }

    [Fact]
    public void A_shared_member_is_only_positioned_once()
    {
        const string yaml = """
            services:
              api:
                image: nginx
                networks:
                  - back
                  - front
              db:
                image: postgres
                networks:
                  - back
            networks:
              back:
              front:
            """;

        var layout = new GridCityLayoutEngine().Arrange(BuildFrom(yaml));

        Assert.Equal(2, layout.Nodes.Count);
        Assert.Equal(2, layout.Districts.Count);
    }

    [Fact]
    public void Bounds_enclose_members_placed_by_another_district()
    {
        var layout = Sample();
        var map = SampleMap();

        // Every district, overlay or not, must contain all of its members.
        foreach (var bounds in layout.Districts)
        {
            var district = map.Districts.Single(candidate => candidate.Name == bounds.DistrictName);

            Assert.All(district.Members, member =>
            {
                var point = layout.Nodes[member.Name];
                Assert.InRange(point.X, bounds.X, bounds.X + bounds.Width);
                Assert.InRange(point.Y, bounds.Y, bounds.Y + bounds.Height);
            });
        }
    }

    [Fact]
    public void Rebound_with_the_original_positions_changes_nothing()
    {
        var map = SampleMap();
        var engine = new GridCityLayoutEngine();

        var arranged = engine.Arrange(map);
        var rebound = engine.Rebound(map, arranged.Nodes);

        Assert.Equal(arranged.Nodes, rebound.Nodes);
        Assert.Equal(arranged.Districts, rebound.Districts);
        Assert.Equal(arranged.Width, rebound.Width);
        Assert.Equal(arranged.Height, rebound.Height);
    }

    [Fact]
    public void A_district_follows_a_member_that_moved()
    {
        var map = SampleMap();
        var engine = new GridCityLayoutEngine();

        var positions = engine.Arrange(map).Nodes
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);

        // Drag postgres far to the right; fnp-network has to stretch with it.
        positions["postgres"] = new LayoutPoint(1500, positions["postgres"].Y);

        var rebound = engine.Rebound(map, positions);
        var network = rebound.Districts.Single(district => district.DistrictName == "fnp-network");

        Assert.True(network.X + network.Width > 1500);
        Assert.True(rebound.Width > 1500);
    }

    [Fact]
    public void Rebound_keeps_the_overlay_flag()
    {
        const string yaml = """
            services:
              api:
                image: nginx
                networks:
                  - back
                  - front
              db:
                image: postgres
                networks:
                  - back
            networks:
              back:
              front:
            """;

        var map = BuildFrom(yaml);
        var engine = new GridCityLayoutEngine();
        var positions = engine.Arrange(map).Nodes;

        var rebound = engine.Rebound(map, positions);

        Assert.True(rebound.Districts.Single(d => d.DistrictName == "front").IsOverlay);
        Assert.False(rebound.Districts.Single(d => d.DistrictName == "back").IsOverlay);
    }

    [Fact]
    public void Every_link_in_the_sample_file_can_be_routed()
    {
        var map = SampleMap();
        var layout = new GridCityLayoutEngine().Arrange(map);
        var router = new LinkRouter();

        Assert.NotEmpty(map.Links);
        Assert.All(map.Links, link => Assert.NotNull(router.Route(link, layout.Nodes)));
    }

    [Fact]
    public void Options_change_the_result()
    {
        var wide = new GridCityLayoutEngine().Arrange(
            SampleMap(),
            LayoutOptions.Default with { MaxColumns = 8 });

        // Eight members now fit on one row, so the block is wider and shorter.
        var network = wide.Districts.Single(district => district.DistrictName == "fnp-network");

        Assert.True(network.Width > 616);
        Assert.True(network.Height < 328);
    }
}
