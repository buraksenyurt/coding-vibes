using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DoDiApp.Models
{
    public class Term
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [StringLength(1000)]
        public string Definition { get; set; } = string.Empty;

        [Required]
        [StringLength(50)]
        public string MainDomain { get; set; } = string.Empty;

        [StringLength(50)]
        public string? SubDomain { get; set; }

        public bool IsApproved { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [Required]
        public string CreatedBy { get; set; } = string.Empty;

        public string? ApprovedBy { get; set; }

        public DateTime? ApprovedAt { get; set; }

        public int Version { get; set; } = 1;
    }
}