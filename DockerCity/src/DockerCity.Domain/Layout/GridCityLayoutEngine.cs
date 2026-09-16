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
        var districts = OrderDistricts(map).ToList();
        var placedSomething = new HashSet<string>(StringComparer.Ordinal);

        var cursorY = settings.Margin;

        foreach (var district in districts)
        {
            // A service belongs to one block only; it cannot be in two places
            // at once. Districts that share it get an overlay region instead.
            var fresh = district.Members
                .Where(member => !nodes.ContainsKey(member.Name))
                .OrderBy(member => member.Name, StringComparer.Ordinal)
                .Select(member => member.Name)
                .ToList();

            if (fresh.Count == 0)
            {
                continue;
            }

            var block = PlaceBlock(fresh, settings, cursorY, nodes);

            placedSomething.Add(district.Name);
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
            cursorY += block.Height + settings.DistrictSpacing;
        }

        // Bounds are derived from where the members actually ended up, so a
        // district encloses all of its members even when another district
        // placed some of them.
        var bounds = districts
            .Select(district => BoundsFor(district, nodes, settings, placedSomething))
            .OfType<DistrictBounds>()
            .ToList();

        var right = bounds.Count > 0 ? bounds.Max(area => area.X + area.Width) : settings.Margin;
        var bottom = Math.Max(cursorY - settings.DistrictSpacing, settings.Margin);

        return new CityLayout(nodes, bounds, right + settings.Margin, bottom + settings.Margin);
    }

    // Explicit districts first, in name order; the implicit default network
    // always sits at the bottom because it is the leftovers.
    private static IEnumerable<District> OrderDistricts(CityMap map) =>
        map.Districts
           .OrderBy(district => district.IsImplicit)
           .ThenBy(district => district.Name, StringComparer.Ordinal);

    private static DistrictBounds? BoundsFor(
        District district,
        IReadOnlyDictionary<string, LayoutPoint> nodes,
        LayoutOptions settings,
        HashSet<string> placedSomething)
    {
        var points = district.Members
            .Where(member => nodes.ContainsKey(member.Name))
            .Select(member => nodes[member.Name])
            .ToList();

        if (points.Count == 0)
        {
            return null;
        }

        var left = points.Min(point => point.X) - settings.DistrictPadding;
        var top = points.Min(point => point.Y) - settings.DistrictPadding;
        var right = points.Max(point => point.X) + settings.NodeWidth + settings.DistrictPadding;
        var bottom = points.Max(point => point.Y) + settings.NodeHeight + settings.DistrictPadding;

        return new DistrictBounds(
            district.Name,
            district.IsImplicit,
            left,
            top,
            right - left,
            bottom - top)
        {
            IsOverlay = !placedSomething.Contains(district.Name)
        };
    }

    private static (double Width, double Height) PlaceBlock(
        IReadOnlyList<string> names,
        LayoutOptions settings,
        double blockTop,
        Dictionary<string, LayoutPoint> nodes)
    {
        var columns = Math.Min(settings.MaxColumns, names.Count);
        var rows = (int)Math.Ceiling(names.Count / (double)columns);

        var cellWidth = settings.NodeWidth + settings.NodeSpacing;
        var cellHeight = settings.NodeHeight + settings.NodeSpacing;

        for (var index = 0; index < names.Count; index++)
        {
            var column = index % columns;
            var row = index / columns;

            nodes[names[index]] = new LayoutPoint(
                settings.Margin + settings.DistrictPadding + (column * cellWidth),
                blockTop + settings.DistrictPadding + (row * cellHeight));
        }

        var width = (columns * cellWidth) - settings.NodeSpacing + (settings.DistrictPadding * 2);
        var height = (rows * cellHeight) - settings.NodeSpacing + (settings.DistrictPadding * 2);

        return (width, height);
    }
}
