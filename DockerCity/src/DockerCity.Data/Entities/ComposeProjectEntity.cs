namespace DockerCity.Data.Entities;

public class ComposeProjectEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string? FileHash { get; set; }
    public DateTimeOffset LastOpenedAt { get; set; }

    public List<ServiceLayoutEntity> Layouts { get; set; } = [];
}
