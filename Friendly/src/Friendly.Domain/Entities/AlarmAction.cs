using Friendly.Domain.Common;
using Friendly.Domain.Enums;

namespace Friendly.Domain.Entities;

public class AlarmAction : BaseEntity
{
    public ActionType Type { get; set; }
    public string Message { get; set; } = string.Empty;
    public bool IsExecuted { get; set; }
    public DateTime? ExecutedAt { get; set; }

    // Foreign key
    public Guid AlarmId { get; set; }
    
    // Navigation property
    public virtual Alarm Alarm { get; set; } = null!;
}
