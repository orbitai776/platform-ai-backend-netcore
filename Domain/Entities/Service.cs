// AdminService.Domain/Entities/ServiceEntity.cs
using System.ComponentModel.DataAnnotations.Schema;

namespace AdminService.Domain.Entities;

[Table("services")]
public class Service
{
    [Column("id")]
    public Guid Id { get; set; }

    [Column("name")]
    public string Name { get; set; } = string.Empty;

    // tour | villa | product
    [Column("type")]
    public string Type { get; set; } = string.Empty;

    [Column("description")]
    public string? Description { get; set; }

    [Column("default_config", TypeName = "jsonb")]
    public string? DefaultConfig { get; set; }

    // active | deprecated
    [Column("status")]
    public string Status { get; set; } = "active";

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; }
}