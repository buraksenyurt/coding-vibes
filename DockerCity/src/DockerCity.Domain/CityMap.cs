namespace DockerCity.Domain;

public enum CityLinkKind
{
    DependsOn,
    SharedVolume
}

public sealed record CityLink(CityLinkKind Kind, string From, string To);

public sealed class District(string name, bool isImplicit, IReadOnlyList<ComposeService> members) : ComposeElement(name)
{
    public bool IsImplicit { get; } = isImplicit;

    public IReadOnlyList<ComposeService> Members { get; } = members;

    public override string Describe() => IsImplicit
        ? $"{Name} (implicit) — {Members.Count} service"
        : $"{Name} — {Members.Count} service";
}

public sealed class CityMap
{
    public CityMap(
        IReadOnlyList<ComposeService> services,
        IReadOnlyList<District> districts,
        IReadOnlyList<CityLink> links)
    {
        Services = services;
        Districts = districts;
        Links = links;
    }

    public IReadOnlyList<ComposeService> Services { get; }
    public IReadOnlyList<District> Districts { get; }
    public IReadOnlyList<CityLink> Links { get; }

    public ComposeService? Find(string name) =>
        Services.FirstOrDefault(service => service.Name == name);
}