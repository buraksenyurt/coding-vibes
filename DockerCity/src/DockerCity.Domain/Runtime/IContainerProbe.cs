namespace DockerCity.Domain.Runtime;

// The only thing the application needs from Docker. Keeping it here means the
// view model can be tested with a fake, and the Docker client is one
// replaceable implementation rather than a dependency of everything.
public interface IContainerProbe : IDisposable
{
    Task<IReadOnlyList<ContainerSnapshot>> ListAsync(CancellationToken cancellationToken = default);
}
