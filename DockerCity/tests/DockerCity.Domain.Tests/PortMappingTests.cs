using DockerCity.Domain.Values;

namespace DockerCity.Domain.Tests;

public class PortMappingTests
{
    [Fact]
    public void Splits_host_and_container_port()
    {
        var port = PortMapping.Parse("5050:80");

        Assert.Equal(5050, port.HostStart);
        Assert.Equal(80, port.ContainerStart);
        Assert.False(port.IsRange);
        Assert.True(port.IsPublished);
    }

    [Fact]
    public void Reads_a_port_range()
    {
        var port = PortMapping.Parse("21000-21010:21000-21010");

        Assert.True(port.IsRange);
        Assert.Equal(21000, port.HostStart);
        Assert.Equal(21010, port.HostEnd);
        Assert.Equal(11, port.ContainerEnd - port.ContainerStart + 1);
    }

    [Fact]
    public void Marks_an_unpublished_port()
    {
        var port = PortMapping.Parse("6379");

        Assert.False(port.IsPublished);
        Assert.Null(port.HostStart);
        Assert.Equal(6379, port.ContainerStart);
    }

    [Fact]
    public void Reads_the_host_interface()
    {
        var port = PortMapping.Parse("127.0.0.1:5432:5432");

        Assert.Equal("127.0.0.1", port.HostIp);
        Assert.Equal(5432, port.HostStart);
        Assert.Equal(5432, port.ContainerStart);
    }

    [Theory]
    [InlineData("53:53/udp", "udp")]
    [InlineData("53:53", "tcp")]
    public void Reads_the_protocol_suffix(string raw, string expected)
    {
        Assert.Equal(expected, PortMapping.Parse(raw).Protocol);
    }

    [Fact]
    public void Rejects_a_mapping_it_cannot_understand()
    {
        Assert.Throws<FormatException>(() => PortMapping.Parse("1:2:3:4"));
    }
}
