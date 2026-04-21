using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using platform_ai_backend_netcore.Infrastructure.Cache;

namespace platform_ai_backend_netcore.API.Controllers
{

    [ApiController]
    [Route("debug")]
    public class DebugController(
        ICacheService cache,
        ILogger<DebugController> logger) : ControllerBase
    {
        // 🔥 TEST SET + GET
        [HttpGet("redis")]
        public async Task<IActionResult> TestRedis()
        {
            var key = "test:redis";
            var value = new
            {
                Message = "hello redis",
                Time = DateTime.UtcNow
            };

            await cache.SetAsync(key, value, TimeSpan.FromMinutes(5));

            var cached = await cache.GetAsync<object>(key);

            return Ok(new
            {
                success = cached != null,
                key,
                cached
            });
        }

        // 🔥 TEST DELETE
        [HttpDelete("redis")]
        public async Task<IActionResult> DeleteRedis()
        {
            var key = "test:redis";

            await cache.RemoveAsync(key);

            var cached = await cache.GetAsync<object>(key);

            return Ok(new
            {
                deleted = cached == null
            });
        }

        // 🔥 TEST SCAN PREFIX
        [HttpDelete("redis/prefix")]
        public async Task<IActionResult> DeleteByPrefix()
        {
            var pattern = "AdminService:test:*"; // sửa theo prefix bạn dùng

            await cache.RemoveByPrefixAsync(pattern);

            return Ok(new
            {
                message = $"Deleted keys with pattern: {pattern}"
            });
        }
    }
}