// AdminService.Domain/Entities/TokenTransaction.cs
using System.ComponentModel.DataAnnotations.Schema;

namespace AdminService.Domain.Entities;

[Table("token_transactions")]
public class TokenTransaction
{
    [Column("id")]
    public Guid Id { get; set; }

    [Column("partner_id")]
    public Guid PartnerId { get; set; }

    [Column("partner_service_id")]
    public Guid PartnerServiceId { get; set; }

    [Column("user_id")]
    public Guid? UserId { get; set; }

    // MongoDB conversations._id string
    [Column("conversation_id")]
    public string? ConversationId { get; set; }

    // PG guest_sessions.id string
    [Column("guest_session_id")]
    public string? GuestSessionId { get; set; }

    [Column("tokens_used")]
    public int TokensUsed { get; set; }

    [Column("cost")]
    public decimal Cost { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
}