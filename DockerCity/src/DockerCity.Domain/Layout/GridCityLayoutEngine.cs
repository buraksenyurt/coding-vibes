namespace DockerCity.Domain.Layout;

// Stacks districts top to bottom and lays their members out in a grid.
// Deliberately boring: the first arrangement only has to be readable and
// repeatable, because from here on the user drags figures where they want and
// those positions are what gets stored.
public sealed class GridCityLayoutEngine : ICityLayoutEngine
{
    public CityLayout Arrange(CityMap map, LayoutOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(map);

        var settings = options ?? LayoutOptions.Default;
        var nodes = new Dictionary<string, LayoutPoint>(StringComparer.Ordinal);
        var cursorY = settings.Margin;

        foreach (var district in OrderDistricts(map))
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

            cursorY += PlaceBlock(fresh, settings, cursorY, nodes) + settings.DistrictSpacing;
        }

        // Defensive: anything the districts did not cover still gets a spot.
        var orphans = map.Services
            .Where(service => !nodes.ContainsKey(service.Name))
            .OrderBy(service => service.Name, StringComparer.Ordinal)
            .Select(service => service.Name)
            .ToList();

        if (orphans.Count > 0)
        {
            PlaceBlock(orphans, settings, cursorY, nodes);
        }

        return Rebound(map, nodes, settings);
    }

    public CityLayout Rebound(
        CityMap map,
        IReadOnlyDictionary<string, LayoutPoint> nodes,
        LayoutOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(nodes);

        var settings = options ?? LayoutOptions.Default;
        var claiming = ClaimingDistricts(map);

        var bounds = OrderDistricts(map)
            .Select(district => BoundsFor(district, nodes, settings, claiming))
            .OfType<DistrictBounds>()
            .ToList();

        var right = nodes.Count > 0
            ? nodes.Values.Max(point => point.X) + settings.NodeWidth
            : settings.Margin;

        var bottom = nodes.Count > 0
            ? nodes.Values.Max(point => point.Y) + settings.NodeHeight
            : settings.Margin;

        if (bounds.Count > 0)
        {
            right = Math.Max(right, bounds.Max(area => area.X + area.Width));
            bottom = Math.Max(bottom, bounds.Max(area => area.Y + area.Height));
        }

        return new CityLayout(
            nodes.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal),
            bounds,
            right + settings.Margin,
            bottom + settings.Margin);
    }

    // Explicit districts first, in name order; the implicit default network
    // always sits at the bottom because it is the leftovers.
    private static IEnumerable<District> OrderDistricts(CityMap map) =>
        map.Districts
           .OrderBy(district => district.IsImplicit)
           .ThenBy(district => district.Name, StringComparer.Ordinal);

    // Which districts introduce at least one service no earlier district had.
    // Derived from the map alone, so it means the same thing whether figures
    // are being placed for the first time or restored from storage.
    private static HashSet<string> ClaimingDistricts(CityMap map)
    {
        var claiming = new HashSet<string>(StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var district in OrderDistricts(map))
        {
            var claimed = district.Members.Count(member => seen.Add(member.Name));

            if (claimed > 0)
            {
                claiming.Add(district.Name);
            }
        }

        return claiming;
    }

    private static DistrictBounds? BoundsFor(
        District district,
        IReadOnlyDictionary<string, LayoutPoint> nodes,
        LayoutOptions settings,
        HashSet<string> claiming)
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

        return new DistrictBounds(district.Name, district.IsImplicit, left, top, right - left, bottom - top)
        {
            IsOverlay = !claiming.Contains(district.Name)
        };
    }

    private static double PlaceBlock(
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
            nodes[names[index]] = new LayoutPoint(
                settings.Margin + settings.DistrictPadding + (index % columns * cellWidth),
                blockTop + settings.DistrictPadding + (index / columns * cellHeight));
        }

        return (rows * cellHeight) - settings.NodeSpacing + (settings.DistrictPadding * 2);
    }
}
