using Friendly.Domain.Common;
using Friendly.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace Friendly.Domain.Entities;

public class Detail : BaseEntity
{
    [Required(ErrorMessage = "İletişim türü seçilmelidir")]
    public ContactType Type { get; set; }
    
    [Required(ErrorMessage = "Değer alanı zorunludur")]
    [StringLength(500, ErrorMessage = "Değer en fazla 500 karakter olabilir")]
    public string Value { get; set; } = string.Empty;
    
    [StringLength(100, ErrorMessage = "Etiket en fazla 100 karakter olabilir")]
    public string? Label { get; set; }
    
    [StringLength(1000, ErrorMessage = "Ek bilgi en fazla 1000 karakter olabilir")]
    public string? AdditionalInfo { get; set; }
    
    public bool IsPrimary { get; set; }

    // Foreign key
    [Required(ErrorMessage = "Kişi ID'si zorunludur")]
    public Guid PersonId { get; set; }
    
    // Navigation property - Bu validation'dan muaf tutulmalı
    public virtual Person? Person { get; set; }
}
