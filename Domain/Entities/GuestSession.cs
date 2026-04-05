// AdminService.Domain/Entities/GuestSession.cs
using System.ComponentModel.DataAnnotations.Schema;
using platform_ai_backend_netcore.Domain.Entities;

namespace AdminService.Domain.Entities;

[Table("guest_sessions")]
public class GuestSession
{
    [Column("id")]
    public Guid Id { get; set; }

    [Column("partner_service_id")]
    public Guid PartnerServiceId { get; set; }

    // uuid-v4 tạo ở client, lưu localStorage
    [Column("anonymous_id")]
    public string AnonymousId { get; set; } = string.Empty;

    [Column("device_fingerprint")]
    public string? DeviceFingerprint { get; set; }

    [Column("ip_address")]
    public string? IpAddress { get; set; }

    [Column("user_agent")]
    public string? UserAgent { get; set; }

    // active | expired | converted
    [Column("status")]
    public string Status { get; set; } = "active";

    [Column("converted_user_id")]
    public Guid? ConvertedUserId { get; set; }

    [Column("expires_at")]
    public DateTime ExpiresAt { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    public PartnerService? PartnerService { get; set; }
    public User? ConvertedUser { get; set; }
}