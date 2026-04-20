using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace platform_ai_backend_netcore.Infrastructure.Cache
{
    public interface ICacheService
    {
        Task<T?> GetAsync<T>(string key) where T : class;
        Task SetAsync<T>(string key, T value, TimeSpan ttl) where T : class;
        Task RemoveAsync(params string[] keys);
        Task RemoveByPrefixAsync(string prefix);
    }
}