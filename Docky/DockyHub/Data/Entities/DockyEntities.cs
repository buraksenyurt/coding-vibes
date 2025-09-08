using System.ComponentModel.DataAnnotations;

namespace DockyHub.Data.Entities;

public class ServiceEntity
{
    [Key]
    public int Id { get; set; }
    
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;
    
    [MaxLength(500)]
    public string Description { get; set; } = string.Empty;
    
    [Required]
    public string Ports { get; set; } = "[]"; // JSON array
    
    [Required]
    public string DockerComposeContent { get; set; } = string.Empty;
    
    [MaxLength(50)]
    public string Version { get; set; } = "latest";
    
    public string Environment { get; set; } = "{}"; // JSON object
    
    public string Volumes { get; set; } = "[]"; // JSON array
    
    public string Networks { get; set; } = "[]"; // JSON array
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    public bool IsActive { get; set; } = true;
}

public class ModelEntity
{
    [Key]
    public int Id { get; set; }
    
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;
    
    [MaxLength(500)]
    public string Description { get; set; } = string.Empty;
    
    [Required]
    public string Services { get; set; } = "[]"; // JSON array of service names
    
    public int TotalServices { get; set; }
    
    public string Ports { get; set; } = "[]"; // JSON array
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    public bool IsActive { get; set; } = true;
}
