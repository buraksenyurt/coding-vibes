using Microsoft.EntityFrameworkCore;

namespace DockerCity.Data;

public static class DatabaseInitializer
{
    // Opens the local database, creating the folder and applying any pending
    // migrations. Callers own the returned context.
    public static async Task<DockerCityDbContext> OpenAsync(CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(DockerCityPaths.DataDirectory);

        var context = DockerCityDbContextFactory.Create(DockerCityPaths.ConnectionString);
        await context.Database.MigrateAsync(cancellationToken);

        return context;
    }
}
