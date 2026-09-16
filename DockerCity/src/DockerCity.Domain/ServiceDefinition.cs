using DockerCity.Domain.Values;

namespace DockerCity.Domain;
public sealed record ServiceDefinition
{
    public required ImageRef Image { get; init; }
    public string? ContainerName { get; init; }
    public IReadOnlyList<PortMapping> Ports { get; init; } = [];
    public IReadOnlyList<EnvVariable> Environment { get; init; } = [];
    public IReadOnlyList<VolumeMount> Volumes { get; init; } = [];
    public IReadOnlyList<string> NetworkNames { get; init; } = [];
    public IReadOnlyList<string> DependsOn { get; init; } = [];
    public CommandSpec? Command { get; init; }
    public RestartPolicy Restart { get; init; } = RestartPolicy.No;
}

public enum CommandKind { Shell, Exec }

public sealed record CommandSpec(CommandKind Kind, string? Raw, IReadOnlyList<string> Arguments);