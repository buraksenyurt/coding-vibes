namespace DockerCity.Domain.Runtime;

// The lifecycle states Docker reports for a container. "Unknown" is ours: it
// means we have not been told anything, not that Docker said so.
public enum ContainerState
{
    Unknown,
    Created,
    Running,
    Paused,
    Restarting,
    Exited,
    Dead
}
