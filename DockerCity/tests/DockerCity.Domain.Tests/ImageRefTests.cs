using DockerCity.Domain.Values;

namespace DockerCity.Domain.Tests;

public class ImageRefTests
{
    [Theory]
    [InlineData("postgres:latest", null, "postgres", "latest")]
    [InlineData("postgres", null, "postgres", "latest")]
    [InlineData("rabbitmq:3-management", null, "rabbitmq", "3-management")]
    [InlineData("dpage/pgadmin4:latest", null, "dpage/pgadmin4", "latest")]
    [InlineData("qdrant/qdrant", null, "qdrant/qdrant", "latest")]
    [InlineData("quay.io/keycloak/keycloak:latest", "quay.io", "keycloak/keycloak", "latest")]
    public void Parses_image_reference(string raw, string? registry, string repository, string tag)
    {
        var image = ImageRef.Parse(raw);

        Assert.Equal(registry, image.Registry);
        Assert.Equal(repository, image.Repository);
        Assert.Equal(tag, image.Tag);
    }

    [Fact]
    public void First_segment_without_a_dot_is_a_namespace_not_a_registry()
    {
        // "qdrant" is a Docker Hub account name.
        Assert.Null(ImageRef.Parse("qdrant/qdrant").Registry);

        // "quay.io" contains a dot, so it is a registry host.
        Assert.Equal("quay.io", ImageRef.Parse("quay.io/keycloak/keycloak").Registry);
    }

    [Fact]
    public void Registry_with_a_port_is_recognised()
    {
        var image = ImageRef.Parse("localhost:5000/myapp:1.2");

        Assert.Equal("localhost:5000", image.Registry);
        Assert.Equal("myapp", image.Repository);
        Assert.Equal("1.2", image.Tag);
    }

    [Fact]
    public void Digest_is_kept_and_suppresses_the_latest_flag()
    {
        var image = ImageRef.Parse("postgres@sha256:abc123");

        Assert.Equal("sha256:abc123", image.Digest);
        Assert.False(image.IsLatest);
    }

    [Theory]
    [InlineData("postgres:latest", true)]
    [InlineData("qdrant/qdrant", true)]
    [InlineData("rabbitmq:3-management", false)]
    public void Detects_the_latest_tag(string raw, bool expected)
    {
        Assert.Equal(expected, ImageRef.Parse(raw).IsLatest);
    }

    [Fact]
    public void Display_name_drops_the_namespace()
    {
        Assert.Equal("pgadmin4", ImageRef.Parse("dpage/pgadmin4:latest").DisplayName);
        Assert.Equal("postgres", ImageRef.Parse("postgres:latest").DisplayName);
    }
}
