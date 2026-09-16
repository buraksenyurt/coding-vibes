using DockerCity.Domain.Values;

namespace DockerCity.Domain.Services;

public sealed class IdentityService : ComposeService, IWebAccessible
{
    public IdentityService(string name, ServiceDefinition definition)
        : base(name, definition) { }

    public override ServiceCategory Category => ServiceCategory.Identity;

    public PortMapping? WebPort => PublishedPorts.FirstOrDefault();

    public Uri? WebUrl => LocalEndpoint.For(WebPort);

    public override string Describe() => WebUrl is null
        ? $"{Name} — identity provider"
        : $"{Name} — identity provider, console {WebUrl}";
}
