using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace platform_ai_backend_netcore.Infrastructure.Cache
{
    public class RedisHealthCheck : IHealthCheck
    {
        private readonly IConnectionMultiplexer _redis;

        public RedisHealthCheck(IConnectionMultiplexer redis)
        {
            _redis = redis;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            try
            {
                if (!_redis.IsConnected)
                    return HealthCheckResult.Unhealthy("Redis disconnected");

                var ping = await _redis.GetDatabase().PingAsync();

                return HealthCheckResult.Healthy($"Latency: {ping.TotalMilliseconds}ms");
            }
            catch (Exception ex)
            {
                return HealthCheckResult.Unhealthy("Redis error", ex);
            }
        }
    }
}