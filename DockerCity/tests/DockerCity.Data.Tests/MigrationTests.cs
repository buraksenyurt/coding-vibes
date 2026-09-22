using DockerCity.Domain.Catalog;
using Microsoft.EntityFrameworkCore;

namespace DockerCity.Data.Tests;

// Every other test builds its schema with EnsureCreated, straight from the
// model. That tests the model and says nothing about the migrations the app
// actually runs, which is how a missing migration once slipped through with
// the whole suite green. These two close that gap.
public class MigrationTests
{
    [Fact]
    public void The_model_has_no_changes_missing_from_a_migration()
    {
        using var context = DockerCityDbContextFactory.Create("Data Source=:memory:");

        // Fails the moment an entity changes without "dotnet ef migrations add".
        // The app would otherwise refuse to start: since EF Core 9, Migrate()
        // throws on pending model changes.
        Assert.False(
            context.Database.HasPendingModelChanges(),
            "The model changed but no migration was added. Run: "
            + "dotnet ef migrations add <Name> --project src\\DockerCity.Data --startup-project src\\DockerCity.Data");
    }

    [Fact]
    public async Task Migrations_build_a_working_database_from_scratch()
    {
        var file = Path.Combine(Path.GetTempPath(), $"dockercity-migrate-{Guid.NewGuid():N}.db");

        try
        {
            await using (var context = DockerCityDbContextFactory.Create(DockerCityPaths.ToConnectionString(file)))
            {
                await context.Database.MigrateAsync();

                Assert.Empty(await context.Database.GetPendingMigrationsAsync());
                Assert.Equal(InMemoryImageCatalog.Defaults.Count, await context.ImageMappings.CountAsync());

                // Touches the newest column, so a migration that forgot it fails here.
                Assert.Empty(await context.ComposeProjects.Where(project => project.IsHiddenFromRecent).ToListAsync());
            }
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

            try
            {
                File.Delete(file);
            }
            catch (IOException)
            {
            }
        }
    }
}
