using System.Globalization;

namespace DockerCity.Domain.Values;

public sealed record PortMapping(
    string? HostIp,
    int? HostStart,
    int? HostEnd,
    int ContainerStart,
    int ContainerEnd,
    string Protocol)
{
    public bool IsRange => ContainerEnd > ContainerStart;
    public bool IsPublished => HostStart.HasValue;

    public static PortMapping Parse(string raw)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(raw);

        var value = raw.Trim();
        var protocol = "tcp";

        var slash = value.LastIndexOf('/');
        if (slash >= 0)
        {
            protocol = value[(slash + 1)..];
            value = value[..slash];
        }

        var parts = value.Split(':');

        return parts.Length switch
        {
            1 => Build(null, null, parts[0], protocol),
            2 => Build(null, parts[0], parts[1], protocol),
            3 => Build(parts[0], parts[1], parts[2], protocol),

            _ => throw new FormatException($"Port mapping can not be parsed: '{raw}'")
        };
    }

    private static PortMapping Build(string? hostIp, string? host, string container, string protocol)
    {
        var (containerStart, containerEnd) = ParseRange(container);

        if (host is null)
            return new PortMapping(hostIp, null, null, containerStart, containerEnd, protocol);

        var (hostStart, hostEnd) = ParseRange(host);
        return new PortMapping(hostIp, hostStart, hostEnd, containerStart, containerEnd, protocol);
    }

    private static (int Start, int End) ParseRange(string text)
    {
        var dash = text.IndexOf('-');

        if (dash < 0)
        {
            var single = int.Parse(text, CultureInfo.InvariantCulture);
            return (single, single);
        }

        return (
            int.Parse(text[..dash], CultureInfo.InvariantCulture),
            int.Parse(text[(dash + 1)..], CultureInfo.InvariantCulture));
    }
}