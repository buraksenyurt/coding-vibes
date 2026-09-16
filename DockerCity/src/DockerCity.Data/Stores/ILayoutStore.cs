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
}
