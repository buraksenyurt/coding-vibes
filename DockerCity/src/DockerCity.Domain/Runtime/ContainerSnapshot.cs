namespace DockerCity.Domain.Runtime;

// One container as Docker described it at one moment. Everything here comes
// from a single "list containers" call: no inspect, no per-container round
// trip. The parsing lives in this type so the Docker client stays a thin
// adapter and all of this can be tested without a daemon.
//
// Id and Name are constructor parameters rather than required properties:
// a container without them is not a container, and this way the compiler
// asks for them at every creation site instead of trusting an initializer.
public sealed record ContainerSnapshot(string Id, string Name)
{
    // Compose stamps these on every container it starts.
    public const string ProjectLabel = "com.docker.compose.project";
    public const string ServiceLabel = "com.docker.compose.service";
    public const string ConfigFilesLabel = "com.docker.compose.project.config_files";

    public string? ProjectName { get; init; }

    public string? ServiceName { get; init; }

    // Absolute paths of the compose files this container was started from,
    // comma separated. The most precise thing we have to match on.
    public string? ConfigFiles { get; init; }

    public ContainerState State { get; init; } = ContainerState.Unknown;

    public HealthState Health { get; init; } = HealthState.None;

    // Docker's own wording: "Up 3 hours (healthy)", "Exited (0) 2 days ago".
    public string StatusText { get; init; } = string.Empty;

    public string Image { get; init; } = string.Empty;

    public bool IsUp => State is ContainerState.Running or ContainerState.Restarting;

    public RuntimeSignal Signal => State switch
    {
        ContainerState.Running when Health is HealthState.Unhealthy => RuntimeSignal.Unhealthy,
        ContainerState.Running when Health is HealthState.Starting => RuntimeSignal.Starting,
        ContainerState.Running when Health is HealthState.Healthy => RuntimeSignal.Healthy,
        ContainerState.Running => RuntimeSignal.Running,
        ContainerState.Restarting => RuntimeSignal.Starting,
        ContainerState.Paused => RuntimeSignal.Paused,
        ContainerState.Created or ContainerState.Exited or ContainerState.Dead => RuntimeSignal.Stopped,
        _ => RuntimeSignal.Unknown
    };

    // Labels arrive as whatever dictionary the client hands over - Docker.DotNet
    // uses IDictionary, which is not an IReadOnlyDictionary - so the loosest
    // shape both sides can agree on is a sequence of pairs.
    public static ContainerSnapshot From(
        string id,
        IEnumerable<string>? names,
        IEnumerable<KeyValuePair<string, string>>? labels,
        string? state,
        string? status,
        string? image)
    {
        var label = Lookup(labels);

        return new ContainerSnapshot(id ?? string.Empty, CleanName(names))
        {
            ProjectName = Label(label, ProjectLabel),
            ServiceName = Label(label, ServiceLabel),
            ConfigFiles = Label(label, ConfigFilesLabel),
            State = ParseState(state),
            Health = ParseHealth(status),
            StatusText = status?.Trim() ?? string.Empty,
            Image = image?.Trim() ?? string.Empty
        };
    }

    public static ContainerState ParseState(string? state) => state?.Trim().ToLowerInvariant() switch
    {
        "created" => ContainerState.Created,
        "running" => ContainerState.Running,
        "paused" => ContainerState.Paused,
        "restarting" => ContainerState.Restarting,
        "exited" or "removing" => ContainerState.Exited,
        "dead" => ContainerState.Dead,
        _ => ContainerState.Unknown
    };

    // The list endpoint does not return health as a field; it only writes it
    // into the human-readable status. Reading it from there costs one string
    // search, while asking properly would cost one inspect call per container
    // on every poll.
    public static HealthState ParseHealth(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return HealthState.None;
        }

        if (status.Contains("(healthy)", StringComparison.OrdinalIgnoreCase))
        {
            return HealthState.Healthy;
        }

        if (status.Contains("(unhealthy)", StringComparison.OrdinalIgnoreCase))
        {
            return HealthState.Unhealthy;
        }

        return status.Contains("health: starting", StringComparison.OrdinalIgnoreCase)
            ? HealthState.Starting
            : HealthState.None;
    }

    // Docker returns names with a leading slash, and a container can have
    // several; the first one is the one people see.
    private static string CleanName(IEnumerable<string>? names) =>
        names?.Select(name => name.TrimStart('/')).FirstOrDefault(name => !string.IsNullOrWhiteSpace(name)) ?? string.Empty;

    private static Dictionary<string, string> Lookup(IEnumerable<KeyValuePair<string, string>>? labels)
    {
        var lookup = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var pair in labels ?? [])
        {
            // Label keys are unique in practice; last one wins if they are not.
            lookup[pair.Key] = pair.Value;
        }

        return lookup;
    }

    private static string? Label(Dictionary<string, string> labels, string key) =>
        labels.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : null;
}
