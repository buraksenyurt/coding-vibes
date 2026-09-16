namespace DockerCity.Data.Stores;

public interface ILayoutStore
{
    Task<IReadOnlyDictionary<string, ServicePosition>> LoadAsync(
        int composeProjectId,
        CancellationToken cancellationToken = default);

    Task SaveAsync(
        int composeProjectId,
        string serviceName,
        ServicePosition position,
        CancellationToken cancellationToken = default);

    // Forgets every stored position for a project, so the next open falls back
    // to the computed arrangement.
    Task<int> ClearAsync(int composeProjectId, CancellationToken cancellationToken = default);
}
