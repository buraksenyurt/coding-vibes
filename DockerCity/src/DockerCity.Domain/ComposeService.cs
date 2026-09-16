using DockerCity.Domain.Values;

namespace DockerCity.Domain;

public enum ServiceCategory
{
    Unknown,
    Database,
    Messaging,
    Storage,
    Identity,
    Tooling,
    Gateway
}

public enum RestartPolicy
{
    No,
    Always,
    OnFailure,
    UnlessStopped
}

public abstract class ComposeService : ComposeElement
{
    protected ComposeService(string name, ServiceDefinition definition) : base(name)
    {
        ArgumentNullException.ThrowIfNull(definition);

        Image = definition.Image;
        ContainerName = definition.ContainerName;
        Ports = definition.Ports;
        Environment = definition.Environment;
        Volumes = definition.Volumes;
        NetworkNames = definition.NetworkNames;
        DependsOn = definition.DependsOn;
        Command = definition.Command;
        Restart = definition.Restart;
    }

    public ImageRef Image { get; }
    public string? ContainerName { get; }

    public IReadOnlyList<PortMapping> Ports { get; }
    public IReadOnlyList<EnvVariable> Environment { get; }
    public IReadOnlyList<VolumeMount> Volumes { get; }
    public IReadOnlyList<string> NetworkNames { get; }
    public IReadOnlyList<string> DependsOn { get; }
    public CommandSpec? Command { get; }
    public RestartPolicy Restart { get; }
    public abstract ServiceCategory Category { get; }
    public virtual string IconKey => Image.DisplayName;
    public bool IsSupervised => Restart is RestartPolicy.Always or RestartPolicy.UnlessStopped;

    public IReadOnlyList<PortMapping> PublishedPorts =>
        [.. Ports.Where(port => port.IsPublished)];

    public override string Describe() =>
        $"{Name} — {Image.Repository}:{Image.Tag}";
}