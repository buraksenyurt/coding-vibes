using DockerCity.Domain;
using DockerCity.Domain.Catalog;

namespace DockerCity.Data.Entities;

public class ImageMappingEntity
{
    public int Id { get; set; }
    public string Pattern { get; set; } = string.Empty;
    public MatchMode MatchMode { get; set; }
    public ServiceCategory Category { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string IconFileName { get; set; } = string.Empty;
    public int Priority { get; set; }
}
