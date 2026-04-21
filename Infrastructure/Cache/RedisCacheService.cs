// Infrastructure/Cache/RedisCacheService.cs
using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace platform_ai_backend_netcore.Infrastructure.Cache;

public class RedisCacheService(
    IDistributedCache          cache,
    IConnectionMultiplexer     redis,
    ILogger<RedisCacheService> logger) : ICacheService
{
    
    private static readonly JsonSerializerOptions _json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    // O(1) — StackExchange.Redis track trạng thái nội bộ, không gọi network.
    // Khi Redis down: IsConnected = false → skip hoàn toàn, không block.
    private bool IsConnected => redis.IsConnected;

    // ── GET ────────────────────────────────────────────────────
    public async Task<T?> GetAsync<T>(string key) where T : class
    {
        if (!IsConnected)
        {
            logger.LogDebug("[CACHE SKIP] GET key={Key} — Redis not connected", key);
            return null;
        }

        var sw = Stopwatch.StartNew();
        try
        {
            var raw = await cache.GetStringAsync(key);
            sw.Stop();

            if (raw is null)
            {
                logger.LogDebug("[CACHE MISS] key={Key} elapsed={ElapsedMs}ms", key, sw.ElapsedMilliseconds);
                return null;
            }

            logger.LogDebug("[CACHE HIT]  key={Key} elapsed={ElapsedMs}ms", key, sw.ElapsedMilliseconds);
            return JsonSerializer.Deserialize<T>(raw, _json);
        }
        catch (Exception ex)
        {
            sw.Stop();
            logger.LogWarning(ex, "[CACHE ERROR] GET key={Key} — fallback to DB", key);
            return null;
        }
    }

    // ── SET ────────────────────────────────────────────────────
    public async Task SetAsync<T>(string key, T value, TimeSpan ttl) where T : class
    {
        if (!IsConnected)
        {
            logger.LogDebug("[CACHE SKIP] SET key={Key} — Redis not connected", key);
            return;
        }

        try
        {
            var raw  = JsonSerializer.Serialize(value, _json);
            var opts = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = ttl,
            };
            await cache.SetStringAsync(key, raw, opts);
            logger.LogDebug("[CACHE SET]  key={Key} ttl={Ttl}", key, ttl);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[CACHE ERROR] SET key={Key}", key);
        }
    }

    // ── REMOVE ─────────────────────────────────────────────────
    public async Task RemoveAsync(params string[] keys)
    {
        if (!IsConnected) return;
        await Task.WhenAll(keys.Select(RemoveOneAsync));
    }

    // ── REMOVE BY PREFIX (SCAN pattern) ────────────────────────
    /// <summary>
    /// Xoá tất cả key khớp với pattern — pattern phải là raw Redis key (có InstancePrefix).
    /// Dùng các *ScanPattern() method từ CacheKeys thay vì tự build string.
    ///
    /// Tại sao cần InstancePrefix trong pattern?
    ///   IDistributedCache.SetStringAsync("development:partner:list:...")
    ///   → Redis thực tế lưu: "AdminService:development:partner:list:..."
    ///   → SCAN phải dùng pattern "AdminService:development:partner:list:*" mới match.
    /// </summary>
    public async Task RemoveByPrefixAsync(string scanPattern)
    {
        if (!IsConnected) return;

        try
        {
            var db     = redis.GetDatabase();
            var server = redis.GetServers().FirstOrDefault(s => s.IsConnected && !s.IsReplica);
            if (server is null) return;

            var keys = server.Keys(pattern: scanPattern).ToArray();
            if (keys.Length == 0) return;

            await db.KeyDeleteAsync(keys);
            logger.LogDebug("[CACHE EVICT] pattern={Pattern} count={Count}", scanPattern, keys.Length);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[CACHE ERROR] RemoveByPrefix pattern={Pattern}", scanPattern);
        }
    }

    private async Task RemoveOneAsync(string key)
    {
        try
        {
            await cache.RemoveAsync(key);
            logger.LogDebug("[CACHE DEL]  key={Key}", key);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[CACHE ERROR] REMOVE key={Key}", key);
        }
    }
}
