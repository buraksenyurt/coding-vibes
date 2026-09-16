using DockerCity.Domain;
using DockerCity.Domain.Catalog;
using DockerCity.Domain.Values;

namespace DockerCity.Data.Tests;

public class SqliteImageCatalogTests
{
    [Theory]
    [InlineData("postgres:latest", ServiceCategory.Database)]
    [InlineData("rabbitmq:3-management", ServiceCategory.Messaging)]
    [InlineData("minio/minio:latest", ServiceCategory.Storage)]
    [InlineData("quay.io/keycloak/keycloak:latest", ServiceCategory.Identity)]
    [InlineData("dpage/pgadmin4:latest", ServiceCategory.Tooling)]
    [InlineData("nginx:alpine", ServiceCategory.Gateway)]
    [InlineData("some-vendor/unheard-of:1.0", ServiceCategory.Unknown)]
    public async Task Database_backed_catalog_matches_the_built_in_one(string image, ServiceCategory expected)
    {
        using var database = new TemporaryDatabase();

        var fromDatabase = new CatalogServiceCategoryResolver(
            await SqliteImageCatalog.LoadAsync(database.Context));
        var fromMemory = new InMemoryServiceCategoryResolver();

        var reference = ImageRef.Parse(image);

        Assert.Equal(expected, fromDatabase.Resolve(reference));
        Assert.Equal(expected, fromMemory.Resolve(reference));
    }

    [Fact]
    public async Task Exact_rule_outranks_the_family_fallback()
    {
        using var database = new TemporaryDatabase();
        var catalog = await SqliteImageCatalog.LoadAsync(database.Context);

        var exact = catalog.Match(ImageRef.Parse("postgres:16"));
        var family = catalog.Match(ImageRef.Parse("timescale/timescaledb-postgres:latest"));

        Assert.NotNull(exact);
        Assert.Equal(MatchMode.Exact, exact!.MatchMode);

        // Only the Contains rule can reach a vendor build.
        Assert.NotNull(family);
        Assert.Equal(MatchMode.Contains, family!.MatchMode);
        Assert.Equal(ServiceCategory.Database, family.Category);
    }

    [Fact]
    public async Task Editing_a_row_changes_what_the_catalog_resolves()
    {
        using var database = new TemporaryDatabase();

        var row = database.Context.ImageMappings
            .Single(mapping => mapping.Pattern == "sonarqube" && mapping.MatchMode == MatchMode.Exact);
        row.Category = ServiceCategory.Gateway;
        await database.Context.SaveChangesAsync();

        using var reader = database.NewConnection();
        var resolver = new CatalogServiceCategoryResolver(
            await SqliteImageCatalog.LoadAsync(reader));

        Assert.Equal(ServiceCategory.Gateway, resolver.Resolve(ImageRef.Parse("sonarqube:latest")));
    }
}
