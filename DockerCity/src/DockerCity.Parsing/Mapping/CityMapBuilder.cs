using DockerCity.Domain;
using DockerCity.Domain.Values;
using DockerCity.Parsing.Dto;

namespace DockerCity.Parsing.Mapping;

public sealed class CityMapBuilder
{
    public const string DefaultNetworkName = "default";

    private readonly ServiceFactory _factory;

    public CityMapBuilder(ServiceFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        _factory = factory;
    }

    public CityMap Build(ComposeFileDto file)
    {
        ArgumentNullException.ThrowIfNull(file);

        var services = file.Services
            .Select(entry => _factory.Create(entry.Key, entry.Value))
            .ToList();

        return new CityMap(services, BuildDistricts(file, services), BuildLinks(services));
    }

    private static List<District> BuildDistricts(
        ComposeFileDto file,
        IReadOnlyList<ComposeService> services)
    {
        var districts = new List<District>();

        foreach (var networkName in file.Networks.Keys)
        {
            var members = services
                .Where(service => service.NetworkNames.Contains(networkName))
                .ToList();

            districts.Add(new District(networkName, isImplicit: false, members));
        }

        var orphans = services.Where(service => service.NetworkNames.Count == 0).ToList();

        if (orphans.Count > 0)
        {
            var existing = districts.FirstOrDefault(d => d.Name == DefaultNetworkName);

            if (existing is null)
            {
                districts.Add(new District(DefaultNetworkName, isImplicit: true, orphans));
            }
            else
            {
                districts.Remove(existing);
                districts.Add(new District(
                    DefaultNetworkName,
                    isImplicit: false,
                    [.. existing.Members, .. orphans]));
            }
        }

        return districts;
    }

    private static List<CityLink> BuildLinks(IReadOnlyList<ComposeService> services)
    {
        var links = new List<CityLink>();
        var names = services.Select(service => service.Name).ToHashSet(StringComparer.Ordinal);

        // depends_on is directed: the dependent service points at what it needs.
        foreach (var service in services)
        {
            foreach (var target in service.DependsOn)
            {
                if (!names.Contains(target))
                {
                    throw new InvalidOperationException(
                        $"Service '{service.Name}' depends on '{target}', which is not defined.");
                }

                links.Add(new CityLink(CityLinkKind.DependsOn, service.Name, target));
            }
        }

        // Two services that mount the same named volume share storage.
        // Undirected, so each  pair is emitted once.
        var byVolume = services
            .SelectMany(service => service.Volumes
                .Where(volume => volume.Kind == VolumeMountKind.Named && volume.Source is not null)
                .Select(volume => (Volume: volume.Source!, Service: service.Name)))
            .GroupBy(pair => pair.Volume, StringComparer.Ordinal);

        foreach (var group in byVolume)
        {
            var users = group.Select(pair => pair.Service).Distinct(StringComparer.Ordinal)
                             .OrderBy(name => name, StringComparer.Ordinal).ToList();

            for (var i = 0; i < users.Count; i++)
            {
                for (var j = i + 1; j < users.Count; j++)
                    links.Add(new CityLink(CityLinkKind.SharedVolume, users[i], users[j]));
            }
        }

        return links;
    }
}