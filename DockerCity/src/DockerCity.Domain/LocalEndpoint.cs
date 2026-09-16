using DockerCity.Domain.Values;

namespace DockerCity.Domain;

internal static class LocalEndpoint
{
    public static Uri? For(PortMapping? port) => port is { HostStart: int host }
        ? new Uri($"http://localhost:{host}")
        : null;
}
