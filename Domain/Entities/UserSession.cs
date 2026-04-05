using System.ComponentModel.DataAnnotations.Schema;

namespace platform_ai_backend_netcore.Domain.Entities
{
    [Table("user_sessions")]
public class UserSession
{
    [Column("id")]
    public Guid Id { get; set; }

    [Column("user_id")]
    public Guid UserId { get; set; }

    [Column("device_fingerprint")]
    public string? DeviceFingerprint { get; set; }

    [Column("device_name")]
    public string? DeviceName { get; set; }

    [Column("ip_address")]
    public string? IpAddress { get; set; }

    [Column("user_agent")]
    public string? UserAgent { get; set; }

    [Column("refresh_token_hash")]
    public string RefreshTokenHash { get; set; } = string.Empty;

    [Column("last_active_at")]
    public DateTime? LastActiveAt { get; set; }

    [Column("expires_at")]
    public DateTime ExpiresAt { get; set; }

    // true = bị kick
    [Column("is_revoked")]
    public bool IsRevoked { get; set; } = false;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    public User? User { get; set; }
}
}