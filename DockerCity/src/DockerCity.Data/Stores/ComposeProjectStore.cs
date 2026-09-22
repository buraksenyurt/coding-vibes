using DockerCity.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace DockerCity.Data.Stores;

public sealed class ComposeProjectStore(DockerCityDbContext context) : IComposeProjectStore
{
    public async Task<ComposeProjectEntity> OpenAsync(
        string filePath,
        string? fileHash = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var project = await context.ComposeProjects
            .SingleOrDefaultAsync(candidate => candidate.FilePath == filePath, cancellationToken);

        if (project is null)
        {
            project = new ComposeProjectEntity
            {
                FilePath = filePath,
                Name = Path.GetFileName(Path.GetDirectoryName(filePath)) is { Length: > 0 } folder
                    ? folder
                    : Path.GetFileNameWithoutExtension(filePath)
            };

            context.ComposeProjects.Add(project);
        }

        project.FileHash = fileHash ?? project.FileHash;
        project.LastOpenedAt = DateTimeOffset.UtcNow;
        project.IsHiddenFromRecent = false;

        await context.SaveChangesAsync(cancellationToken);

        return project;
    }

    public async Task<ComposeProjectEntity?> FindAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        return await context.ComposeProjects
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.FilePath == filePath, cancellationToken);
    }

    public async Task<IReadOnlyList<ComposeProjectEntity>> RecentAsync(
        int count = 10,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);

        return await context.ComposeProjects
            .AsNoTracking()
            .Where(project => !project.IsHiddenFromRecent)
            .OrderByDescending(project => project.LastOpenedAt)
            .Take(count)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> ClearRecentAsync(CancellationToken cancellationToken = default)
    {
        var hidden = await context.ComposeProjects
            .Where(project => !project.IsHiddenFromRecent)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(project => project.IsHiddenFromRecent, true),
                cancellationToken);

        // ExecuteUpdate bypasses the change tracker; entities it already holds
        // would otherwise keep reporting the old value.
        context.ChangeTracker.Clear();

        return hidden;
    }

    public async Task<int> RemoveFromRecentAsync(string filePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var hidden = await context.ComposeProjects
            .Where(project => project.FilePath == filePath && !project.IsHiddenFromRecent)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(project => project.IsHiddenFromRecent, true),
                cancellationToken);

        context.ChangeTracker.Clear();

        return hidden;
    }
}
