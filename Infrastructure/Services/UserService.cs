// Infrastructure/Services/UserService.cs
using AdminService.Application.Common;
using AdminService.Application.Users;
using AdminService.Application.Users.DTOs;
using Microsoft.EntityFrameworkCore;
using platform_ai_backend_netcore.Application.Users.DTOs;
using platform_ai_backend_netcore.Infrastructure.Cache;
using platform_ai_backend_netcore.Infrastructure.Data;

namespace platform_ai_backend_netcore.Infrastructure.Services;

public class UserService(
    AppDbContext              db,
    ICacheService             cache,
    ILogger<UserService>      logger) : IUserService
{
    private static readonly TimeSpan _listTtl   = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan _detailTtl = TimeSpan.FromMinutes(10);

    // ── GET ALL ────────────────────────────────────────────────
    public async Task<PagedResult<UserDto>> GetAllAsync(QueryUserDto query)
    {
        var key    = CacheKeys.UserList(query.Page, query.Limit,
                                        query.Status ?? "", query.Search ?? "");
        var cached = await cache.GetAsync<PagedResult<UserDto>>(key);
        if (cached is not null)
        {
            logger.LogDebug("[UserService] GetAll served from cache | page={Page}", query.Page);
            return cached;
        }

        var q = db.Users.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Status))
            q = q.Where(u => u.Status == query.Status.ToLower());

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var kw = query.Search.ToLower();
            q = q.Where(u =>
                (u.FullName != null && u.FullName.ToLower().Contains(kw)) ||
                (u.Email    != null && u.Email.ToLower().Contains(kw)));
        }

        // FIX: EF Core DbContext không thread-safe — KHÔNG dùng Task.WhenAll với cùng 1 DbContext.
        // Chạy tuần tự: count trước, data sau.
        var total = await q.CountAsync();
        var data  = await q
            .OrderByDescending(u => u.CreatedAt)
            .Skip((query.Page - 1) * query.Limit)
            .Take(query.Limit)
            .Select(u => new UserDto
            {
                Id        = u.Id,
                Email     = u.Email,
                FullName  = u.FullName,
                AvatarUrl = u.AvatarUrl,
                Status    = u.Status,
                CreatedAt = u.CreatedAt,
                UpdatedAt = u.UpdatedAt,
            })
            .ToListAsync();

        var result = new PagedResult<UserDto>
        {
            Data  = data,
            Total = total,
            Page  = query.Page,
            Limit = query.Limit,
        };

        await cache.SetAsync(key, result, _listTtl);

        logger.LogInformation(
            "[UserService] GetAll | total={Total} page={Page} limit={Limit}",
            result.Total, query.Page, query.Limit);

        return result;
    }

    // ── GET BY ID ──────────────────────────────────────────────
    public async Task<ServiceResult<UserDetailDto>> GetByIdAsync(Guid id)
    {
        var key    = CacheKeys.UserDetail(id);
        var cached = await cache.GetAsync<UserDetailDto>(key);
        if (cached is not null)
        {
            logger.LogDebug("[UserService] GetById cache hit | userId={UserId}", id);
            return ServiceResult<UserDetailDto>.Ok(cached);
        }

        var user = await db.Users
            .AsNoTracking()
            .Include(u => u.Sessions
                .Where(s => !s.IsRevoked && s.ExpiresAt > DateTime.UtcNow)
                .OrderByDescending(s => s.LastActiveAt))
            .FirstOrDefaultAsync(u => u.Id == id);

        if (user is null)
        {
            logger.LogWarning("[UserService] GetById not found | userId={UserId}", id);
            return ServiceResult<UserDetailDto>.Fail("User not found");
        }

        var dto = new UserDetailDto
        {
            Id             = user.Id,
            FirebaseUid    = user.FirebaseUid,
            Email          = user.Email,
            FullName       = user.FullName,
            AvatarUrl      = user.AvatarUrl,
            Status         = user.Status,
            CreatedAt      = user.CreatedAt,
            UpdatedAt      = user.UpdatedAt,
            ActiveSessions = user.Sessions.Select(s => new UserSessionDto
            {
                Id           = s.Id,
                DeviceName   = s.DeviceName,
                IpAddress    = s.IpAddress,
                LastActiveAt = s.LastActiveAt,
                ExpiresAt    = s.ExpiresAt,
                IsRevoked    = s.IsRevoked,
                CreatedAt    = s.CreatedAt,
            }).ToList(),
        };

        await cache.SetAsync(key, dto, _detailTtl);

        logger.LogInformation(
            "[UserService] GetById | userId={UserId} email={Email} activeSessions={Sessions}",
            user.Id, user.Email, dto.ActiveSessions.Count);

        return ServiceResult<UserDetailDto>.Ok(dto);
    }

    // ── UPDATE ─────────────────────────────────────────────────
    public async Task<ServiceResult<UserDto>> UpdateAsync(Guid id, UpdateUserDto dto)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id);
        if (user is null)
        {
            logger.LogWarning("[UserService] Update not found | userId={UserId}", id);
            return ServiceResult<UserDto>.Fail("User not found");
        }

        var oldStatus = user.Status;
        if (!string.IsNullOrWhiteSpace(dto.Status))   user.Status   = dto.Status.ToLower();
        if (!string.IsNullOrWhiteSpace(dto.FullName)) user.FullName = dto.FullName;

        user.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        await cache.RemoveAsync(CacheKeys.UserDetail(id));
        await cache.RemoveByPrefixAsync(CacheKeys.UserListPrefix);

        logger.LogInformation(
            "[UserService] Update | userId={UserId} oldStatus={OldStatus} newStatus={NewStatus}",
            id, oldStatus, user.Status);

        return ServiceResult<UserDto>.Ok(new UserDto
        {
            Id        = user.Id,
            Email     = user.Email,
            FullName  = user.FullName,
            AvatarUrl = user.AvatarUrl,
            Status    = user.Status,
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt,
        }, "User updated successfully");
    }

    // ── DELETE ─────────────────────────────────────────────────
    public async Task<ServiceResult<bool>> DeleteAsync(Guid id)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id);
        if (user is null)
        {
            logger.LogWarning("[UserService] Delete not found | userId={UserId}", id);
            return ServiceResult<bool>.Fail("User not found");
        }

        user.Status    = "deleted";
        user.UpdatedAt = DateTime.UtcNow;

        var sessions = await db.UserSessions
            .Where(s => s.UserId == id && !s.IsRevoked)
            .ToListAsync();

        sessions.ForEach(s => s.IsRevoked = true);
        await db.SaveChangesAsync();

        await cache.RemoveAsync(CacheKeys.UserDetail(id));
        await cache.RemoveByPrefixAsync(CacheKeys.UserListPrefix);

        logger.LogInformation(
            "[UserService] Delete (soft) | userId={UserId} revokedSessions={Count}",
            id, sessions.Count);

        return ServiceResult<bool>.Ok(true);
    }
}