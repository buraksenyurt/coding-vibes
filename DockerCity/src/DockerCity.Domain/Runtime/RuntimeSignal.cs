namespace DockerCity.Domain.Runtime;

// What the figure shows: state and health folded into one traffic light.
// The view only has to map this to a colour.
public enum RuntimeSignal
{
    // No container was matched, or nobody has looked yet.
    Unknown,

    // Up, and either healthy or without a healthcheck.
    Running,
    Healthy,

    // Up, but the healthcheck is failing or has not passed yet.
    Unhealthy,
    Starting,

    Paused,

    // Created, exited or dead: the container exists but is not serving.
    Stopped
}
