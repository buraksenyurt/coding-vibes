using DockerCity.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace DockerCity.Data.Stores;

public sealed class AppSettingsStore(DockerCityDbContext context) : IAppSettingsStore
{
    public async Task<string?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var setting = await context.AppSettings
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Key == key, cancellationToken);

        return setting?.Value;
    }

    public async Task SetAsync(string key, string? value, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var setting = await context.AppSettings
            .SingleOrDefaultAsync(candidate => candidate.Key == key, cancellationToken);

        if (setting is null)
        {
            context.AppSettings.Add(new AppSettingEntity { Key = key, Value = value });
        }
        else
        {
            setting.Value = value;
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
