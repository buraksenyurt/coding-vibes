using DockerCity.Data.Entities;

namespace DockerCity.Data.Stores;

public interface IComposeProjectStore
{
    // Returns the stored project for this path, creating it on first sight
    // and refreshing LastOpenedAt either way.
    Task<ComposeProjectEntity> OpenAsync(
        string filePath,
        string? fileHash = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ComposeProjectEntity>> RecentAsync(
        int count = 10,
        CancellationToken cancellationToken = default);
}
