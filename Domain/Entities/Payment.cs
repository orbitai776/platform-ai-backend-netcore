// AdminService.Domain/Entities/Payment.cs
using System.ComponentModel.DataAnnotations.Schema;

namespace AdminService.Domain.Entities;

[Table("payments")]
public class Payment
{
    [Column("id")]
    public Guid Id { get; set; }

    [Column("partner_id")]
    public Guid PartnerId { get; set; }

    [Column("amount")]
    public decimal Amount { get; set; }

    // stripe | payos | bank_transfer | momo | zalopay | grant
    [Column("payment_method")]
    public string? PaymentMethod { get; set; }

    // pending | completed | failed | refunded
    [Column("status")]
    public string Status { get; set; } = "pending";

    // Idempotency key từ payment provider
    [Column("transaction_id")]
    public string? TransactionId { get; set; }

    [Column("token_amount")]
    public int? TokenAmount { get; set; }

    [Column("note")]
    public string? Note { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; }
}