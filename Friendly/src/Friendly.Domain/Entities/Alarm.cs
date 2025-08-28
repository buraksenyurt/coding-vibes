using Friendly.Domain.Common;
using Friendly.Domain.Enums;

namespace Friendly.Domain.Entities;

public class Alarm : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public AlarmCriteria Criteria { get; set; }
    public DateTime TriggerDate { get; set; }
    public int DaysBefore { get; set; }
    public bool IsActive { get; set; }
    public bool IsRecurring { get; set; }

    // Foreign key
    public Guid PersonId { get; set; }
    
    // Navigation properties
    public virtual Person Person { get; set; } = null!;
    public virtual ICollection<AlarmAction> Actions { get; set; } = new List<AlarmAction>();
}
