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

        await context.SaveChangesAsync(cancellationToken);

        return project;
    }

    public async Task<IReadOnlyList<ComposeProjectEntity>> RecentAsync(
        int count = 10,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);

        return await context.ComposeProjects
            .AsNoTracking()
            .OrderByDescending(project => project.LastOpenedAt)
            .Take(count)
            .ToListAsync(cancellationToken);
    }
}
