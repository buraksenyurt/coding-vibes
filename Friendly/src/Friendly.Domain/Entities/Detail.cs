using Friendly.Domain.Common;
using Friendly.Domain.Enums;

namespace Friendly.Domain.Entities;

public class Detail : BaseEntity
{
    public ContactType Type { get; set; }
    public string Value { get; set; } = string.Empty;
    public string? Label { get; set; }
    public string? AdditionalInfo { get; set; }
    public bool IsPrimary { get; set; }

    // Foreign key
    public Guid PersonId { get; set; }
    
    // Navigation property
    public virtual Person Person { get; set; } = null!;
}
