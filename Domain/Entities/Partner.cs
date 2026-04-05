// AdminService.Domain/Entities/Partner.cs
using System.ComponentModel.DataAnnotations.Schema;
using platform_ai_backend_netcore.Domain.Entities;

namespace AdminService.Domain.Entities;

[Table("partners")]
public class Partner
{
    [Column("id")]
    public Guid Id { get; set; }

    [Column("owner_user_id")]
    public Guid? OwnerUserId { get; set; }

    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Column("description")]
    public string? Description { get; set; }

    [Column("email")]
    public string? Email { get; set; }

    [Column("phone")]
    public string? Phone { get; set; }

    [Column("address")]
    public string? Address { get; set; }

    // pending | active | suspended
    [Column("status")]
    public string Status { get; set; } = "pending";

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; }

    public User? OwnerUser { get; set; }
    public List<PartnerService> PartnerServices { get; set; } = [];
    public List<TokenTransaction> TokenTransactions { get; set; } = [];
    public List<Payment> Payments { get; set; } = [];
}