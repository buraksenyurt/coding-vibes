using Docker.DotNet;
using Docker.DotNet.Models;
using DockerCity.Domain.Runtime;

namespace DockerCity.Live;

// The one place that knows Docker.DotNet exists.
//
// It asks a single question - "list every container" - and hands the answers
// to the domain to interpret. No inspect calls, no filters: a filter would
// have to guess the project name, and being wrong there would hide a
// container rather than show it as stopped.
public sealed class DockerContainerProbe : IContainerProbe
{
    private readonly DockerClient _client;

    public DockerContainerProbe()
        : this(LocalEndpoint())
    {
    }

    public DockerContainerProbe(Uri endpoint)
    {
        ArgumentNullException.ThrowIfNull(endpoint);

        _client = new DockerClientConfiguration(endpoint).CreateClient();
    }

    // Docker Desktop on Windows listens on a named pipe; everywhere else it
    // is a Unix socket. Neither is an HTTP port, which is why this cannot run
    // from anywhere but the machine Docker is on.
    public static Uri LocalEndpoint() => OperatingSystem.IsWindows()
        ? new Uri("npipe://./pipe/docker_engine")
        : new Uri("unix:///var/run/docker.sock");

    public async Task<IReadOnlyList<ContainerSnapshot>> ListAsync(CancellationToken cancellationToken = default)
    {
        // All = true: a stopped container is news, not silence.
        var containers = await _client.Containers
            .ListContainersAsync(new ContainersListParameters { All = true }, cancellationToken)
            .ConfigureAwait(false);

        return containers
            .Select(container => ContainerSnapshot.From(
                container.ID,
                container.Names,
                container.Labels,
                container.State,
                container.Status,
                container.Image))
            .ToList();
    }

    public void Dispose() => _client.Dispose();
}
