using DockerCity.Domain.Values;

namespace DockerCity.Domain.Tests;

public class VolumeMountTests
{
    [Fact]
    public void Reads_a_named_volume()
    {
        var mount = VolumeMount.Parse("postgres_data:/var/lib/postgres/data");

        Assert.Equal(VolumeMountKind.Named, mount.Kind);
        Assert.Equal("postgres_data", mount.Source);
        Assert.Equal("/var/lib/postgres/data", mount.Target);
        Assert.False(mount.IsReadOnly);
    }

    [Theory]
    [InlineData("./config:/etc/app")]
    [InlineData("/var/run/docker.sock:/var/run/docker.sock")]
    [InlineData("~/data:/data")]
    public void Reads_a_bind_mount(string raw)
    {
        Assert.Equal(VolumeMountKind.Bind, VolumeMount.Parse(raw).Kind);
    }

    [Fact]
    public void Reads_an_anonymous_volume()
    {
        var mount = VolumeMount.Parse("/var/lib/data");

        Assert.Equal(VolumeMountKind.Anonymous, mount.Kind);
        Assert.Null(mount.Source);
        Assert.Equal("/var/lib/data", mount.Target);
    }

    [Fact]
    public void Reads_the_read_only_flag()
    {
        Assert.True(VolumeMount.Parse("config:/etc/app:ro").IsReadOnly);
        Assert.False(VolumeMount.Parse("config:/etc/app:rw").IsReadOnly);
    }
}
