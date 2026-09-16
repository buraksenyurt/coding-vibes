using DockerCity.Data;
using Microsoft.EntityFrameworkCore;

namespace DockerCity.Data.Tests;

// A throwaway SQLite file per test. EnsureCreated builds the schema straight
// from the model and applies HasData, so these tests exercise the model
// itself rather than the migration that ships it.
public sealed class TemporaryDatabase : IDisposable
{
    private readonly string _file;

    public TemporaryDatabase()
    {
        _file = Path.Combine(Path.GetTempPath(), $"dockercity-{Guid.NewGuid():N}.db");
        Context = DockerCityDbContextFactory.Create(DockerCityPaths.ToConnectionString(_file));
        Context.Database.EnsureCreated();
    }

    public DockerCityDbContext Context { get; }

    public DockerCityDbContext NewConnection()
    {
        var context = DockerCityDbContextFactory.Create(DockerCityPaths.ToConnectionString(_file));
        return context;
    }

    public void Dispose()
    {
        Context.Dispose();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

        try
        {
            if (File.Exists(_file))
            {
                File.Delete(_file);
            }
        }
        catch (IOException)
        {
            // A temp file left behind is not worth failing a test over.
        }
    }
}
