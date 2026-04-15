// Infrastructure/Services/UserService.cs
using System.Text.Json;
using AdminService.Application.Common;
using AdminService.Application.Users;
using AdminService.Application.Users.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using platform_ai_backend_netcore.Application.Users.DTOs;
using platform_ai_backend_netcore.Infrastructure.Data;

namespace platform_ai_backend_netcore.Infrastructure.Services;

public class UserService(AppDbContext db, IDistributedCache cache) : IUserService
{
    // List cache: 5 phút — chấp nhận stale nhẹ
    private static readonly DistributedCacheEntryOptions _listOpts = new()
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
    };

    // Detail cache: 10 phút
    private static readonly DistributedCacheEntryOptions _detailOpts = new()
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10)
    };

    // ── GET ALL ───────────────────────────────────────────────
    public async Task<PagedResult<UserDto>> GetAllAsync(QueryUserDto query)
    {
        var cacheKey = $"list:page={query.Page}:limit={query.Limit}" +
                       $":status={query.Status ?? ""}:search={query.Search ?? ""}";

        var cached = await SafeGetCacheAsync(cacheKey);
        if (cached is not null)
            return JsonSerializer.Deserialize<PagedResult<UserDto>>(cached)!;

        var q = db.Users.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Status))
            q = q.Where(u => u.Status == query.Status.ToLower());

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var kw = query.Search.ToLower();
            q = q.Where(u =>
                (u.FullName != null && u.FullName.ToLower().Contains(kw)) ||
                (u.Email != null && u.Email.ToLower().Contains(kw)));
        }

        var total = await q.CountAsync();
        var data = await q
            .OrderByDescending(u => u.CreatedAt)
            .Skip((query.Page - 1) * query.Limit)
            .Take(query.Limit)
            .Select(u => new UserDto
            {
                Id = u.Id,
                Email = u.Email,
                FullName = u.FullName,
                AvatarUrl = u.AvatarUrl,
                Status = u.Status,
                CreatedAt = u.CreatedAt,
                UpdatedAt = u.UpdatedAt,
            })
            .ToListAsync();

        var result = new PagedResult<UserDto>
        {
            Data = data,
            Total = total,
            Page = query.Page,
            Limit = query.Limit,
        };

        await SafeSetCacheAsync(cacheKey, JsonSerializer.Serialize(result), _listOpts);

        return result;
    }

    // ── GET BY ID ─────────────────────────────────────────────
    public async Task<ServiceResult<UserDetailDto>> GetByIdAsync(Guid id)
    {
        var cacheKey = $"detail:{id}";

        var cached = await SafeGetCacheAsync(cacheKey);
        if (cached is not null)
            return ServiceResult<UserDetailDto>.Ok(
                JsonSerializer.Deserialize<UserDetailDto>(cached)!);

        var user = await db.Users
            .AsNoTracking()
            .Include(u => u.Sessions
                .Where(s => !s.IsRevoked && s.ExpiresAt > DateTime.UtcNow)
                .OrderByDescending(s => s.LastActiveAt))
            .FirstOrDefaultAsync(u => u.Id == id);

        if (user is null)
            return ServiceResult<UserDetailDto>.Fail("User not found");

        var dto = new UserDetailDto
        {
            Id = user.Id,
            FirebaseUid = user.FirebaseUid,
            Email = user.Email,
            FullName = user.FullName,
            AvatarUrl = user.AvatarUrl,
            Status = user.Status,
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt,
            ActiveSessions = user.Sessions.Select(s => new UserSessionDto
            {
                Id = s.Id,
                DeviceName = s.DeviceName,
                IpAddress = s.IpAddress,
                LastActiveAt = s.LastActiveAt,
                ExpiresAt = s.ExpiresAt,
                IsRevoked = s.IsRevoked,
                CreatedAt = s.CreatedAt,
            }).ToList(),
        };

        await SafeSetCacheAsync(cacheKey, JsonSerializer.Serialize(dto), _detailOpts);

        return ServiceResult<UserDetailDto>.Ok(dto);
    }

    // ── UPDATE ────────────────────────────────────────────────
    public async Task<ServiceResult<UserDto>> UpdateAsync(Guid id, UpdateUserDto dto)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id);
        if (user is null)
            return ServiceResult<UserDto>.Fail("User not found");

        if (!string.IsNullOrWhiteSpace(dto.Status))
            user.Status = dto.Status.ToLower();

        if (!string.IsNullOrWhiteSpace(dto.FullName))
            user.FullName = dto.FullName;

        user.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        // Xóa detail cache — list cache tự expire sau 5 phút
        await SafeRemoveCacheAsync($"detail:{id}");

        var userDto = new UserDto
        {
            Id = user.Id,
            Email = user.Email,
            FullName = user.FullName,
            AvatarUrl = user.AvatarUrl,
            Status = user.Status,
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt,
        };

        // Trả về success kèm message và data
        return ServiceResult<UserDto>.Ok(userDto, "User updated successfully");
    }

    // ── DELETE ────────────────────────────────────────────────
    public async Task<ServiceResult<bool>> DeleteAsync(Guid id)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id);
        if (user is null)
            return ServiceResult<bool>.Fail("User not found");

        user.Status = "deleted";
        user.UpdatedAt = DateTime.UtcNow;

        var sessions = await db.UserSessions
            .Where(s => s.UserId == id && !s.IsRevoked)
            .ToListAsync();

        sessions.ForEach(s => s.IsRevoked = true);

        await db.SaveChangesAsync();

        await SafeRemoveCacheAsync($"detail:{id}");

        return ServiceResult<bool>.Ok(true);
    }

    // ── SAFE CACHE HELPERS ────────────────────────────────────
    // Wrap tất cả Redis call trong try/catch
    // Nếu Redis down → fallback về DB, không crash service
    private async Task<string?> SafeGetCacheAsync(string key)
    {
        try { return await cache.GetStringAsync(key); }
        catch { return null; }
    }

    private async Task SafeSetCacheAsync(string key, string value,
        DistributedCacheEntryOptions opts)
    {
        try { await cache.SetStringAsync(key, value, opts); }
        catch { /* Redis down → bỏ qua, không crash */ }
    }

    private async Task SafeRemoveCacheAsync(string key)
    {
        try { await cache.RemoveAsync(key); }
        catch { /* Redis down → bỏ qua */ }
    }
}