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

    // Kiểm tra Redis có đang connected không — KHÔNG gọi network, chỉ đọc trạng thái nội bộ.
    // StackExchange.Redis track connection state internally → IsConnected là O(1), không block.
    // Khi Redis down: IsConnected = false → skip hoàn toàn, không tốn 1ms nào.
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

    // ── REMOVE BY PREFIX ───────────────────────────────────────
    public async Task RemoveByPrefixAsync(string prefix)
    {
        if (!IsConnected) return;

        try
        {
            var db     = redis.GetDatabase();
            var server = redis.GetServers().FirstOrDefault(s => s.IsConnected);
            if (server is null) return;

            var keys = server.Keys(pattern: $"{prefix}*").ToArray();
            if (keys.Length == 0) return;

            await db.KeyDeleteAsync(keys);
            logger.LogDebug("[CACHE EVICT] prefix={Prefix} count={Count}", prefix, keys.Length);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[CACHE ERROR] RemoveByPrefix prefix={Prefix}", prefix);
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