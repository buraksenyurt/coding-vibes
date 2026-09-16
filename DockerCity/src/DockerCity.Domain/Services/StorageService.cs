using DockerCity.Domain.Values;

namespace DockerCity.Domain.Services;

public sealed class StorageService : ComposeService, IWebAccessible, IDataPersisting
{
    // Container-side ports that are known to serve a browser console rather than the API.
    private static readonly int[] KnownConsolePorts = [9001, 9090];

    public StorageService(string name, ServiceDefinition definition)
        : base(name, definition) { }

    public override ServiceCategory Category => ServiceCategory.Storage;

    public PortMapping? ApiPort =>
        PublishedPorts.FirstOrDefault(port => !KnownConsolePorts.Contains(port.ContainerStart));

    public PortMapping? WebPort =>
        PublishedPorts.FirstOrDefault(port => KnownConsolePorts.Contains(port.ContainerStart));

    public Uri? WebUrl => LocalEndpoint.For(WebPort);

    public IReadOnlyList<VolumeMount> DataVolumes =>
        [.. Volumes.Where(volume => volume.Kind == VolumeMountKind.Named)];

    public bool HasPersistentData => DataVolumes.Count > 0;

    public override string Describe() => HasPersistentData
        ? $"{Name} — storage, data is persistent"
        : $"{Name} — storage, data is temporary";
}
