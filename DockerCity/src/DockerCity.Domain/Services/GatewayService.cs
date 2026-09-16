using DockerCity.Domain.Values;

namespace DockerCity.Domain.Services;

public sealed class GatewayService : ComposeService, IWebAccessible
{
    public GatewayService(string name, ServiceDefinition definition)
        : base(name, definition) { }

    public override ServiceCategory Category => ServiceCategory.Gateway;

    public PortMapping? WebPort => PublishedPorts.FirstOrDefault();

    public Uri? WebUrl => LocalEndpoint.For(WebPort);

    public override string Describe() => WebUrl is null
        ? $"{Name} — gateway"
        : $"{Name} — gateway, entry point at {WebUrl}";
}
