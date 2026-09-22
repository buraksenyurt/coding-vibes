using DockerCity.Data.Entities;

namespace DockerCity.Data.Stores;

public interface IComposeProjectStore
{
    // Returns the stored project for this path, creating it on first sight,
    // refreshing LastOpenedAt and putting it back on the recent list.
    Task<ComposeProjectEntity> OpenAsync(
        string filePath,
        string? fileHash = null,
        CancellationToken cancellationToken = default);

    // Read-only lookup, used to compare the hash from the last visit with the
    // file as it is now before OpenAsync overwrites it.
    Task<ComposeProjectEntity?> FindAsync(
        string filePath,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ComposeProjectEntity>> RecentAsync(
        int count = 10,
        CancellationToken cancellationToken = default);

    // Both hide rather than delete, so stored layouts survive.
    Task<int> ClearRecentAsync(CancellationToken cancellationToken = default);

    Task<int> RemoveFromRecentAsync(string filePath, CancellationToken cancellationToken = default);
}
