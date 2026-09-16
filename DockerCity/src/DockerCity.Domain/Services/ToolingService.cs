using DockerCity.Domain.Values;

namespace DockerCity.Domain.Services;

public sealed class ToolingService : ComposeService, IWebAccessible
{
    public ToolingService(string name, ServiceDefinition definition)
        : base(name, definition) { }

    public override ServiceCategory Category => ServiceCategory.Tooling;

    public PortMapping? WebPort => PublishedPorts.FirstOrDefault();

    public Uri? WebUrl => LocalEndpoint.For(WebPort);

    public override string Describe() => WebUrl is null
        ? $"{Name} — developer tool"
        : $"{Name} — developer tool, available at {WebUrl}";
}
