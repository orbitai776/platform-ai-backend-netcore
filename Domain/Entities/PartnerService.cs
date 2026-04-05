// AdminService.Domain/Entities/PartnerService.cs
using System.ComponentModel.DataAnnotations.Schema;

namespace AdminService.Domain.Entities;

[Table("partner_services")]
public class PartnerService
{
    [Column("id")]
    public Guid Id { get; set; }

    [Column("partner_id")]
    public Guid PartnerId { get; set; }

    [Column("service_id")]
    public Guid ServiceId { get; set; }

    [Column("name")]
    public string? Name { get; set; }

    [Column("token_limit")]
    public int TokenLimit { get; set; }

    [Column("token_used")]
    public int TokenUsed { get; set; }

    [Column("storage_limit")]
    public int? StorageLimit { get; set; }

    [Column("available_schedule", TypeName = "json")]
    public string? AvailableSchedule { get; set; }

    [Column("config", TypeName = "jsonb")]
    public string? Config { get; set; }

    [Column("status")]
    public string Status { get; set; } = "active";

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; }

    // Navigation
    public Partner? Partner { get; set; }
    public Service? Service { get; set; }
}