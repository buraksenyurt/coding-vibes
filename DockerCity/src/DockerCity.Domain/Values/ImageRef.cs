using System.Globalization;

namespace DockerCity.Domain.Values;

public readonly record struct ImageRef(
    string? Registry,
    string Repository,
    string Tag,
    string? Digest)
{
    public bool IsLatest => Digest is null && Tag == "latest";

    public string DisplayName =>
        Repository.Contains('/') ? Repository[(Repository.LastIndexOf('/') + 1)..] : Repository;

    public static ImageRef Parse(string raw)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(raw);

        var value = raw.Trim();
        string? digest = null;

        var at = value.IndexOf('@');
        if (at >= 0)
        {
            digest = value[(at + 1)..];
            value = value[..at];
        }

        string? registry = null;
        var slash = value.IndexOf('/');
        if (slash > 0)
        {
            var head = value[..slash];
            if (head.Contains('.') || head.Contains(':') || head == "localhost")
            {
                registry = head;
                value = value[(slash + 1)..];
            }
        }

        var tag = "latest";
        var colon = value.LastIndexOf(':');
        if (colon >= 0)
        {
            tag = value[(colon + 1)..];
            value = value[..colon];
        }

        return new ImageRef(registry, value, tag, digest);
    }
}