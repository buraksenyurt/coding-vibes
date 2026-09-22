namespace DockerCity.Domain.Runtime;

// Which running container belongs to which service in the file?
//
// There is no single answer that always works, because a container may have
// been started by this compose file, by a copy of it somewhere else, or by
// hand with a matching name. So this is a list of rules, strongest first, and
// a service takes the first container a rule agrees on.
public static class RuntimeMatcher
{
    public static IReadOnlyDictionary<string, ContainerSnapshot> Match(
        IEnumerable<ComposeService> services,
        string? projectName,
        string? composeFilePath,
        IEnumerable<ContainerSnapshot> containers)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(containers);

        var candidates = containers.ToList();
        var matched = new Dictionary<string, ContainerSnapshot>(StringComparer.Ordinal);

        foreach (var service in services)
        {
            var found = ByConfigFile(candidates, service.Name, composeFilePath)
                     ?? ByProjectLabel(candidates, service.Name, projectName)
                     ?? ByContainerName(candidates, service.ContainerName)
                     ?? ByConventionalName(candidates, service.Name, projectName);

            if (found is not null)
            {
                matched[service.Name] = found;
            }
        }

        return matched;
    }

    // Strongest rule: the container carries the absolute path of the very file
    // that is open. A copy of the same project in another folder cannot pass.
    private static ContainerSnapshot? ByConfigFile(List<ContainerSnapshot> containers, string service, string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var full = Path.GetFullPath(path);

        return Best(containers.Where(container =>
            Same(container.ServiceName, service)
            && container.ConfigFiles is not null
            && container.ConfigFiles
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Any(file => PathsMatch(file, full))));
    }

    private static ContainerSnapshot? ByProjectLabel(List<ContainerSnapshot> containers, string service, string? project)
    {
        // Without a project name, a service label alone would match any
        // project that happens to have a service called "postgres" - and
        // almost every project does.
        if (string.IsNullOrWhiteSpace(project))
        {
            return null;
        }

        return Best(containers.Where(container =>
            Same(container.ServiceName, service) && Same(container.ProjectName, project)));
    }

    // The file asked for a specific name, so Docker had to use it.
    private static ContainerSnapshot? ByContainerName(List<ContainerSnapshot> containers, string? containerName) =>
        string.IsNullOrWhiteSpace(containerName)
            ? null
            : Best(containers.Where(container => Same(container.Name, containerName)));

    // Last resort: the names Compose generates when it is left alone.
    // v1 used project_service_1, v2 uses project-service-1.
    private static ContainerSnapshot? ByConventionalName(List<ContainerSnapshot> containers, string service, string? project)
    {
        if (string.IsNullOrWhiteSpace(project))
        {
            return null;
        }

        var underscore = $"{project}_{service}_";
        var dash = $"{project}-{service}-";

        return Best(containers.Where(container =>
            StartsWith(container.Name, underscore) || StartsWith(container.Name, dash)));
    }

    // A service can leave more than one container behind: yesterday's exited
    // one and today's running one. The living one is the interesting one.
    private static ContainerSnapshot? Best(IEnumerable<ContainerSnapshot> matches) =>
        matches.OrderByDescending(container => container.IsUp).FirstOrDefault();

    private static bool Same(string? left, string? right) =>
        left is not null && right is not null && string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    private static bool StartsWith(string? name, string prefix) =>
        name is not null && name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);

    // Windows paths differ in case and in separator; the label is written by
    // Compose, the other side comes from a file picker.
    private static bool PathsMatch(string left, string right) =>
        string.Equals(
            Path.GetFullPath(left.Trim()).TrimEnd(Path.DirectorySeparatorChar),
            right.TrimEnd(Path.DirectorySeparatorChar),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
}
