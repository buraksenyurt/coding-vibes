using DockerCity.Domain.Values;

namespace DockerCity.Domain.Services;

public sealed class DatabaseService : ComposeService, IDataPersisting
{
    public DatabaseService(string name, ServiceDefinition definition)
        : base(name, definition) { }

    public override ServiceCategory Category => ServiceCategory.Database;

    public IReadOnlyList<VolumeMount> DataVolumes =>
        [.. Volumes.Where(volume => volume.Kind == VolumeMountKind.Named)];

    public bool HasPersistentData => DataVolumes.Count > 0;

    public override string Describe() => HasPersistentData
        ? $"{Name} — data is persistent"
        : $"{Name} — data is temporary";
}