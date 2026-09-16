using DockerCity.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace DockerCity.Data.Stores;

public sealed class LayoutStore(DockerCityDbContext context) : ILayoutStore
{
    public async Task<IReadOnlyDictionary<string, ServicePosition>> LoadAsync(
        int composeProjectId,
        CancellationToken cancellationToken = default)
    {
        var rows = await context.ServiceLayouts
            .AsNoTracking()
            .Where(layout => layout.ComposeProjectId == composeProjectId)
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(
            layout => layout.ServiceName,
            layout => new ServicePosition(layout.X, layout.Y, layout.IsPinned),
            StringComparer.Ordinal);
    }

    public async Task SaveAsync(
        int composeProjectId,
        string serviceName,
        ServicePosition position,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);

        var existing = await context.ServiceLayouts.SingleOrDefaultAsync(
            layout => layout.ComposeProjectId == composeProjectId
                   && layout.ServiceName == serviceName,
            cancellationToken);

        if (existing is null)
        {
            context.ServiceLayouts.Add(new ServiceLayoutEntity
            {
                ComposeProjectId = composeProjectId,
                ServiceName = serviceName,
                X = position.X,
                Y = position.Y,
                IsPinned = position.IsPinned
            });
        }
        else
        {
            existing.X = position.X;
            existing.Y = position.Y;
            existing.IsPinned = position.IsPinned;
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> ClearAsync(int composeProjectId, CancellationToken cancellationToken = default)
    {
        return await context.ServiceLayouts
            .Where(layout => layout.ComposeProjectId == composeProjectId)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
