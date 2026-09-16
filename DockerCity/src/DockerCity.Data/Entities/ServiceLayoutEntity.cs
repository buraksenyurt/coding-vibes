namespace DockerCity.Data.Entities;

public class ServiceLayoutEntity
{
    public int Id { get; set; }
    public int ComposeProjectId { get; set; }
    public ComposeProjectEntity? ComposeProject { get; set; }

    public string ServiceName { get; set; } = string.Empty;
    public double X { get; set; }
    public double Y { get; set; }
    public bool IsPinned { get; set; }
}
