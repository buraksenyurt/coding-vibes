using Friendly.Domain.Common;

namespace Friendly.Domain.Entities;

public class Person : BaseEntity
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public DateTime? BirthDate { get; set; }
    public string? About { get; set; }
    public string? Notes { get; set; }
    public bool IsOffended { get; set; }
    public string? OffendedReason { get; set; }
    public DateTime? OffendedAt { get; set; }

    // Navigation properties
    public virtual ICollection<Detail> Details { get; set; } = new List<Detail>();
    public virtual ICollection<Alarm> Alarms { get; set; } = new List<Alarm>();

    public string FullName => $"{FirstName} {LastName}";
}
