using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AdminService.Application.Common;
using AdminService.Application.Users;
using AdminService.Application.Users.DTOs;
using Microsoft.EntityFrameworkCore;
using platform_ai_backend_netcore.Application.Users.DTOs;
using platform_ai_backend_netcore.Infrastructure.Data;

namespace platform_ai_backend_netcore.Infrastructure.Services
{
    public class UserService(AppDbContext db) : IUserService
    {
        public async Task<PagedResult<UserDto>> GetAllAsync(QueryUserDto query)
        {
            var q = db.Users.AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(query.Status))
            {
                q = q.Where(u => u.Status == query.Status.ToLower());

            }
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
            return new PagedResult<UserDto>
            {
                Data = data,
                Total = total,
                Page = query.Page,
                Limit = query.Limit,
            };
        }
        public async Task<ServiceResult<UserDetailDto>> GetByIdAsync(Guid id)
        {
            var user = await db.Users
                .AsNoTracking()
                .Include(u => u.Sessions
                    .Where(s => !s.IsRevoked && s.ExpiresAt > DateTime.Now)
                    .OrderByDescending(s => s.LastActiveAt))
                .FirstOrDefaultAsync(u => u.Id == id);
            if (user is null) return ServiceResult<UserDetailDto>.Fail("User not found");

            return ServiceResult<UserDetailDto>.Ok(new UserDetailDto
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
            });
        }
        public async Task<ServiceResult<bool>> DeleteAsync(Guid id)
        {
            var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id);
        if (user is null)
            return ServiceResult<bool>.Fail("User not found");

        // Soft delete — theo schema status: deleted
        user.Status    = "deleted";
        user.UpdatedAt = DateTime.UtcNow;

        // Revoke toàn bộ session
        var sessions = await db.UserSessions
            .Where(s => s.UserId == id && !s.IsRevoked)
            .ToListAsync();

        sessions.ForEach(s => s.IsRevoked = true);

        await db.SaveChangesAsync();

        return ServiceResult<bool>.Ok(true);
        }

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

            return ServiceResult<UserDto>.Ok(new UserDto
            {
                Id = user.Id,
                Email = user.Email,
                FullName = user.FullName,
                AvatarUrl = user.AvatarUrl,
                Status = user.Status,
                CreatedAt = user.CreatedAt,
                UpdatedAt = user.UpdatedAt,
            });
        }
    }
}