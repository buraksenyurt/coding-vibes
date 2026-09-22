namespace DockerCity.Data.Entities;

public class ComposeProjectEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string? FileHash { get; set; }
    public DateTimeOffset LastOpenedAt { get; set; }

    // "Clear recent" hides a project instead of deleting it: deleting would
    // cascade to its stored layout and throw away positions the user chose.
    // Named so that false is the natural default; see phase 8 for why a bool
    // with a database default of true is a trap in EF Core.
    public bool IsHiddenFromRecent { get; set; }

    public List<ServiceLayoutEntity> Layouts { get; set; } = [];
}
