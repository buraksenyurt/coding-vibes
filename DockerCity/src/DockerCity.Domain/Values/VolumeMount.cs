namespace DockerCity.Domain.Values;

public enum VolumeMountKind
{
    Named,       // postgres_data:/var/lib/postgres/data
    Bind,        // ./config:/etc/app
    Anonymous    // /var/lib/data
}

public sealed record VolumeMount(
    VolumeMountKind Kind,
    string? Source,
    string Target,
    bool IsReadOnly)
{
    public static VolumeMount Parse(string raw)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(raw);

        var parts = raw.Trim().Split(':');
        var isReadOnly = parts.Length > 2 && parts[^1] == "ro";

        if (parts.Length == 1)
            return new VolumeMount(VolumeMountKind.Anonymous, null, parts[0], isReadOnly);

        var source = parts[0];
        var target = parts[1];

        var kind = source.StartsWith('/') || source.StartsWith('.') || source.StartsWith('~')
            ? VolumeMountKind.Bind
            : VolumeMountKind.Named;

        return new VolumeMount(kind, source, target, isReadOnly);
    }
}