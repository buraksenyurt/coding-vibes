using DockerCity.Domain;
using DockerCity.Domain.Catalog;
using Microsoft.EntityFrameworkCore;

namespace DockerCity.Data.Tests;

public class SeedTests
{
    [Fact]
    public async Task Database_is_seeded_with_the_built_in_catalog()
    {
        using var database = new TemporaryDatabase();

        var stored = await database.Context.ImageMappings.CountAsync();

        Assert.Equal(InMemoryImageCatalog.Defaults.Count, stored);
    }

    [Fact]
    public async Task Seeded_rows_carry_every_field()
    {
        using var database = new TemporaryDatabase();

        var postgres = await database.Context.ImageMappings
            .SingleAsync(mapping => mapping.Pattern == "postgres" && mapping.MatchMode == MatchMode.Exact);

        Assert.Equal(ServiceCategory.Database, postgres.Category);
        Assert.Equal("PostgreSQL", postgres.DisplayName);
        Assert.Equal("postgres.png", postgres.IconFileName);
        Assert.True(postgres.Priority > 0);
    }

    [Fact]
    public async Task Pattern_and_match_mode_together_are_unique()
    {
        using var database = new TemporaryDatabase();

        var duplicates = await database.Context.ImageMappings
            .GroupBy(mapping => new { mapping.Pattern, mapping.MatchMode })
            .Where(group => group.Count() > 1)
            .CountAsync();

        Assert.Equal(0, duplicates);
    }
}
