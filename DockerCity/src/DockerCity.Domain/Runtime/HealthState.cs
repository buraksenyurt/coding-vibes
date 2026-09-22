namespace DockerCity.Domain.Runtime;

// A container only has health if its image or compose file defines a
// healthcheck. "None" is the common case, not a problem.
public enum HealthState
{
    None,
    Starting,
    Healthy,
    Unhealthy
}
