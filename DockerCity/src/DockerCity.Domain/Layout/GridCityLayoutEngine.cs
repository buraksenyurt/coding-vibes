namespace DockerCity.Domain.Layout;

// Stacks districts top to bottom and lays their members out in a grid.
// Deliberately boring: the first arrangement only has to be readable and
// repeatable, because from phase 6 on the user drags figures where they want
// and those positions are what gets stored.
public sealed class GridCityLayoutEngine : ICityLayoutEngine
{
    public CityLayout Arrange(CityMap map, LayoutOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(map);

        var settings = options ?? LayoutOptions.Default;

        var nodes = new Dictionary<string, LayoutPoint>(StringComparer.Ordinal);
        var bounds = new List<DistrictBounds>();

        var cursorY = settings.Margin;
        var widest = 0d;

        foreach (var district in OrderDistricts(map))
        {
            // A service in several districts is drawn in the first one only,
            // otherwise it would need to be in two places at once.
            var members = district.Members
                .Where(member => !nodes.ContainsKey(member.Name))
                .OrderBy(member => member.Name, StringComparer.Ordinal)
                .ToList();

            if (members.Count == 0)
            {
                continue;
            }

            var block = PlaceBlock(members.Select(member => member.Name), settings, cursorY, nodes);

            bounds.Add(new DistrictBounds(
                district.Name,
                district.IsImplicit,
                settings.Margin,
                cursorY,
                block.Width,
                block.Height));

            widest = Math.Max(widest, settings.Margin + block.Width);
            cursorY += block.Height + settings.DistrictSpacing;
        }

        // Defensive: anything the districts did not cover still gets a spot.
        var orphans = map.Services
            .Where(service => !nodes.ContainsKey(service.Name))
            .OrderBy(service => service.Name, StringComparer.Ordinal)
            .Select(service => service.Name)
            .ToList();

        if (orphans.Count > 0)
        {
            var block = PlaceBlock(orphans, settings, cursorY, nodes);
            widest = Math.Max(widest, settings.Margin + block.Width);
            cursorY += block.Height + settings.DistrictSpacing;
        }

        var height = cursorY - settings.DistrictSpacing + settings.Margin;

        return new CityLayout(nodes, bounds, widest + settings.Margin, Math.Max(height, settings.Margin * 2));
    }

    // Explicit districts first, in name order; the implicit default network
    // always sits at the bottom because it is the leftovers.
    private static IEnumerable<District> OrderDistricts(CityMap map) =>
        map.Districts
           .OrderBy(district => district.IsImplicit)
           .ThenBy(district => district.Name, StringComparer.Ordinal);

    private static (double Width, double Height) PlaceBlock(
        IEnumerable<string> names,
        LayoutOptions settings,
        double blockTop,
        Dictionary<string, LayoutPoint> nodes)
    {
        var list = names.ToList();
        var columns = Math.Min(settings.MaxColumns, list.Count);
        var rows = (int)Math.Ceiling(list.Count / (double)columns);

        var cellWidth = settings.NodeWidth + settings.NodeSpacing;
        var cellHeight = settings.NodeHeight + settings.NodeSpacing;

        for (var index = 0; index < list.Count; index++)
        {
            var column = index % columns;
            var row = index / columns;

            nodes[list[index]] = new LayoutPoint(
                settings.Margin + settings.DistrictPadding + (column * cellWidth),
                blockTop + settings.DistrictPadding + (row * cellHeight));
        }

        var width = (columns * cellWidth) - settings.NodeSpacing + (settings.DistrictPadding * 2);
        var height = (rows * cellHeight) - settings.NodeSpacing + (settings.DistrictPadding * 2);

        return (width, height);
    }
}
