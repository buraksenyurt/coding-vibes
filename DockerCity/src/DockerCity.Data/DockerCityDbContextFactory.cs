using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DockerCity.Data;

// Also used by "dotnet ef" at design time: the tool cannot construct a
// context that takes options, so it looks for this factory instead.
public sealed class DockerCityDbContextFactory : IDesignTimeDbContextFactory<DockerCityDbContext>
{
    public DockerCityDbContext CreateDbContext(string[] args) =>
        Create(DockerCityPaths.ConnectionString);

    public static DockerCityDbContext Create(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        var options = new DbContextOptionsBuilder<DockerCityDbContext>()
            .UseSqlite(connectionString)
            .Options;

        return new DockerCityDbContext(options);
    }
}
