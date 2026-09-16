using DockerCity.Domain.Values;

namespace DockerCity.Domain.Services;

public sealed class MessagingService : ComposeService, IWebAccessible
{
    private static readonly int[] KnownManagementPorts = [15672, 8222, 8161];

    public MessagingService(string name, ServiceDefinition definition)
        : base(name, definition) { }

    public override ServiceCategory Category => ServiceCategory.Messaging;

    public PortMapping? BrokerPort =>
        PublishedPorts.FirstOrDefault(port => !KnownManagementPorts.Contains(port.ContainerStart));

    public PortMapping? WebPort =>
        PublishedPorts.FirstOrDefault(port => KnownManagementPorts.Contains(port.ContainerStart));

    public Uri? WebUrl => LocalEndpoint.For(WebPort);

    public override string Describe() => WebPort is null
        ? $"{Name} — messaging"
        : $"{Name} — messaging, management interface {WebUrl}";
}