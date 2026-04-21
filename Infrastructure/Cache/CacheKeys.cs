// Infrastructure/Cache/CacheKeys.cs
namespace platform_ai_backend_netcore.Infrastructure.Cache;

/// <summary>
/// Tập trung toàn bộ Redis cache key — format: {env}:{resource}:{action}:{params}
///
/// ⚠️  QUAN TRỌNG — InstanceName trap:
///     AddStackExchangeRedisCache(otp.InstanceName = "AdminService:") tự prepend
///     "AdminService:" vào MỌI key khi dùng IDistributedCache (Get/Set/Remove).
///     Nhưng RemoveByPrefixAsync dùng IConnectionMultiplexer.SCAN trực tiếp —
///     SCAN thấy raw Redis key nên PHẢI dùng *ScanPattern() (đã include InstancePrefix).
/// </summary>
public static class CacheKeys
{
    /// <summary>
    /// Env prefix — đọc từ OLTP_ENVIROMENT hoặc fallback "development".
    /// Cached tĩnh để tránh gọi Environment.GetEnvironmentVariable mỗi lần build key.
    /// </summary>
    private static readonly string _env =
        (Environment.GetEnvironmentVariable("OLTP_ENVIROMENT") ?? "development").ToLower();

    // Phải khớp với Program.cs: opt.InstanceName = "AdminService:"
    internal const string InstancePrefix = "AdminService:";

    // ── Users ──────────────────────────────────────────────────
    public static string UserList(int page, int limit, string status, string search)
        => $"{_env}:users:list:{page}:{limit}:{status}:{search}";

    public static string UserDetail(Guid id)
        => $"{_env}:users:detail:{id}";

    /// <summary>
    /// Prefix dùng cho RemoveByPrefixAsync — đã include InstancePrefix vì SCAN nhìn raw Redis key.
    /// </summary>
    public static string UserListPrefix => $"{InstancePrefix}{_env}:users:list:";

    /// <summary>Pattern cho SCAN — alias của UserListPrefix + wildcard.</summary>
    public static string UserListScanPattern() => $"{UserListPrefix}*";

    // ── Partners ───────────────────────────────────────────────
    public static string PartnerList(int page, int limit, string status, string search)
        => $"{_env}:partner:list:{page}:{limit}:{status}:{search}";

    public static string PartnerDetail(Guid id)
        => $"{_env}:partner:detail:{id}";

    public static string PartnerTokens(Guid id)
        => $"{_env}:partner:tokens:{id}";

    /// <summary>
    /// Prefix dùng cho RemoveByPrefixAsync — đã include InstancePrefix.
    /// </summary>
    public static string PartnerListPrefix => $"{InstancePrefix}{_env}:partner:list:";

    /// <summary>Pattern cho SCAN — alias của PartnerListPrefix + wildcard.</summary>
    public static string PartnerListScanPattern() => $"{PartnerListPrefix}*";

    // ── Organization ───────────────────────────────────────────
    public static string OrgDetail(Guid partnerId)
        => $"{_env}:org:detail:{partnerId}";

    public static string OrgList(int page, int limit, string status, string search)
        => $"{_env}:org:list:{page}:{limit}:{status}:{search}";

    /// <summary>Prefix dùng cho RemoveByPrefixAsync toàn bộ org cache.</summary>
    public static string OrgListPrefix => $"{InstancePrefix}{_env}:org:";

    /// <summary>Dùng khi cần invalidate toàn bộ org cache (vd: update hàng loạt).</summary>
    public static string OrgListScanPattern() => $"{OrgListPrefix}*";
}