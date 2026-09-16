using DockerCity.Domain.Values;

namespace DockerCity.Domain;

public interface IWebAccessible
{
    PortMapping? WebPort { get; }

    Uri? WebUrl { get; }
}

public interface IDataPersisting
{
    IReadOnlyList<VolumeMount> DataVolumes { get; }

    bool HasPersistentData { get; }
}