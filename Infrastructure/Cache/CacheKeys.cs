// Infrastructure/Cache/CacheKeys.cs
namespace platform_ai_backend_netcore.Infrastructure.Cache;

/// <summary>
/// Tập trung toàn bộ Redis cache key — không magic string rải rác.
/// </summary>
public static class CacheKeys
{
    // ── Users ──────────────────────────────────────────────────
    public static string UserList(int page, int limit, string status, string search)
        => $"users:list:{page}:{limit}:{status}:{search}";

    public static string UserDetail(Guid id) => $"users:detail:{id}";

    public const string UserListPrefix = "users:list:";

    // ── Partners ───────────────────────────────────────────────
    public static string PartnerList(int page, int limit, string status, string search)
        => $"partners:list:{page}:{limit}:{status}:{search}";

    public static string PartnerDetail(Guid id)  => $"partners:detail:{id}";
    public static string PartnerTokens(Guid id)  => $"partners:tokens:{id}";

    public const string PartnerListPrefix = "partners:list:";

    // ── Organization ───────────────────────────────────────────
    public static string OrgDetail(Guid partnerId) => $"org:detail:{partnerId}";
}