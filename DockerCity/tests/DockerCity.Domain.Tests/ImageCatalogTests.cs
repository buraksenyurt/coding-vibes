using DockerCity.Domain.Catalog;
using DockerCity.Domain.Values;

namespace DockerCity.Domain.Tests;

public class ImageCatalogTests
{
    private sealed class FixedCatalog(params ImageMapping[] mappings) : ImageCatalog
    {
        public override IReadOnlyList<ImageMapping> All { get; } = mappings;
    }

    [Theory]
    [InlineData(MatchMode.Exact, "postgres", "postgres", true)]
    [InlineData(MatchMode.Exact, "postgres", "timescale/postgres-ha", false)]
    [InlineData(MatchMode.StartsWith, "qdrant", "qdrant/qdrant", true)]
    [InlineData(MatchMode.Contains, "postgres", "timescale/timescaledb-postgres", true)]
    [InlineData(MatchMode.Contains, "postgres", "dpage/pgadmin4", false)]
    public void Match_mode_decides_what_a_pattern_covers(
        MatchMode mode, string pattern, string repository, bool expected)
    {
        var mapping = new ImageMapping(pattern, mode, ServiceCategory.Database, "x", "x.png");

        Assert.Equal(expected, mapping.Matches(ImageRef.Parse(repository)));
    }

    [Fact]
    public void Matching_ignores_case()
    {
        var mapping = new ImageMapping("Postgres", MatchMode.Exact, ServiceCategory.Database, "x", "x.png");

        Assert.True(mapping.Matches(ImageRef.Parse("POSTGRES:16")));
    }

    [Fact]
    public void Higher_priority_wins()
    {
        var catalog = new FixedCatalog(
            new ImageMapping("redis", MatchMode.Contains, ServiceCategory.Unknown, "family", "a.png", 10),
            new ImageMapping("redis", MatchMode.Exact, ServiceCategory.Database, "exact", "b.png", 100));

        Assert.Equal("exact", catalog.Match(ImageRef.Parse("redis:latest"))!.DisplayName);
    }

    [Fact]
    public void Equal_priority_is_broken_by_the_longer_pattern()
    {
        var catalog = new FixedCatalog(
            new ImageMapping("redis", MatchMode.Contains, ServiceCategory.Database, "short", "a.png"),
            new ImageMapping("redis-stack", MatchMode.Contains, ServiceCategory.Database, "long", "b.png"));

        Assert.Equal("long", catalog.Match(ImageRef.Parse("redis/redis-stack:latest"))!.DisplayName);
    }

    [Fact]
    public void No_rule_means_no_match()
    {
        Assert.Null(new FixedCatalog().Match(ImageRef.Parse("whatever:1")));
    }

    [Fact]
    public void Built_in_catalog_covers_every_service_in_the_sample_file()
    {
        var catalog = new InMemoryImageCatalog();

        string[] sampleImages =
        [
            "postgres:latest",
            "dpage/pgadmin4:latest",
            "rabbitmq:3-management",
            "delfer/alpine-ftp-server",
            "redis:latest",
            "nats:latest",
            "sonarqube:latest",
            "quay.io/keycloak/keycloak:latest",
            "minio/minio:latest",
            "qdrant/qdrant"
        ];

        Assert.All(sampleImages, image => Assert.NotNull(catalog.Match(ImageRef.Parse(image))));
    }
}
