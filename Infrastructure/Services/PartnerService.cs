// Infrastructure/Services/PartnerService.cs
using System.Text.Json;
using AdminService.Application.Common;
using AdminService.Application.Partners;
using AdminService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using platform_ai_backend_netcore.Application.Partners.DTOs;
using platform_ai_backend_netcore.Infrastructure.Data;

namespace platform_ai_backend_netcore.Infrastructure.Services;

public class PartnerService(AppDbContext db, IDistributedCache cache) : IPartnerService
{
    private static readonly DistributedCacheEntryOptions _listOpts = new()
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
    };

    private static readonly DistributedCacheEntryOptions _detailOpts = new()
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10)
    };

    private static readonly DistributedCacheEntryOptions _tokenOpts = new()
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(2) // token thay đổi thường xuyên hơn
    };

    // ── GET ALL ───────────────────────────────────────────────
    public async Task<PagedResult<PartnerDto>> GetAllAsync(QueryPartnerDto query)
    {
        var cacheKey = $"partners:list:page={query.Page}:limit={query.Limit}" +
                       $":status={query.Status ?? ""}:search={query.Search ?? ""}";

        var cached = await SafeGetCacheAsync(cacheKey);
        if (cached is not null)
            return JsonSerializer.Deserialize<PagedResult<PartnerDto>>(cached)!;

        var q = db.Partners
            .AsNoTracking()
            .Include(p => p.OwnerUser)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Status))
            q = q.Where(p => p.Status == query.Status.ToLower());

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var kw = query.Search.ToLower();
            q = q.Where(p =>
                p.Name.ToLower().Contains(kw) ||
                (p.Email != null && p.Email.ToLower().Contains(kw)));
        }

        var total = await q.CountAsync();
        var data  = await q
            .OrderByDescending(p => p.CreatedAt)
            .Skip((query.Page - 1) * query.Limit)
            .Take(query.Limit)
            .Select(p => new PartnerDto
            {
                Id          = p.Id,
                Name        = p.Name,
                Email       = p.Email,
                Phone       = p.Phone,
                Address     = p.Address,
                Description = p.Description,
                Status      = p.Status,
                OwnerEmail  = p.OwnerUser != null ? p.OwnerUser.Email : null,
                OwnerName   = p.OwnerUser != null ? p.OwnerUser.FullName : null,
                CreatedAt   = p.CreatedAt,
                UpdatedAt   = p.UpdatedAt,
            })
            .ToListAsync();

        var result = new PagedResult<PartnerDto>
        {
            Data  = data,
            Total = total,
            Page  = query.Page,
            Limit = query.Limit,
        };

        await SafeSetCacheAsync(cacheKey, JsonSerializer.Serialize(result), _listOpts);

        return result;
    }

    // ── GET BY ID ─────────────────────────────────────────────
    public async Task<ServiceResult<PartnerDetailDto>> GetByIdAsync(Guid id)
    {
        var cacheKey = $"partners:detail:{id}";

        var cached = await SafeGetCacheAsync(cacheKey);
        if (cached is not null)
            return ServiceResult<PartnerDetailDto>.Ok(
                JsonSerializer.Deserialize<PartnerDetailDto>(cached)!);

        var partner = await db.Partners
            .AsNoTracking()
            .AsSplitQuery()
            .Include(p => p.OwnerUser)
            .Include(p => p.PartnerServices)
                .ThenInclude(ps => ps.Service)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (partner is null)
            return ServiceResult<PartnerDetailDto>.Fail("Partner not found");

        var (balance, topup, used) = await CalcTokenBalance(id);

        var recentTx = await db.TokenTransactions
            .AsNoTracking()
            .Where(t => t.PartnerId == id)
            .OrderByDescending(t => t.CreatedAt)
            .Take(10)
            .Select(t => new RecentTransactionDto
            {
                Id             = t.Id,
                TokensUsed     = t.TokensUsed,
                Cost           = t.Cost,
                ConversationId = t.ConversationId,
                GuestSessionId = t.GuestSessionId,
                CreatedAt      = t.CreatedAt,
            })
            .ToListAsync();

        var recentPayments = await db.Payments
            .AsNoTracking()
            .Where(p => p.PartnerId == id)
            .OrderByDescending(p => p.CreatedAt)
            .Take(10)
            .Select(p => new PaymentSummaryDto
            {
                Id            = p.Id,
                Amount        = p.Amount,
                PaymentMethod = p.PaymentMethod,
                Status        = p.Status,
                TokenAmount   = p.TokenAmount,
                Note          = p.Note,
                CreatedAt     = p.CreatedAt,
            })
            .ToListAsync();

        var dto = new PartnerDetailDto
        {
            Id               = partner.Id,
            Name             = partner.Name,
            Email            = partner.Email,
            Phone            = partner.Phone,
            Address          = partner.Address,
            Description      = partner.Description,
            Status           = partner.Status,
            OwnerEmail       = partner.OwnerUser?.Email,
            OwnerName        = partner.OwnerUser?.FullName,
            TokenBalance     = balance,
            TotalTopup       = topup,
            TotalUsed        = used,
            CreatedAt        = partner.CreatedAt,
            UpdatedAt        = partner.UpdatedAt,
            Services         = partner.PartnerServices.Select(ps => new PartnerServiceSummaryDto
            {
                Id          = ps.Id,
                ServiceName = ps.Service?.Name ?? "",
                ServiceType = ps.Service?.Type ?? "",
                CustomName  = ps.Name,
                TokenLimit  = ps.TokenLimit,
                TokenUsed   = ps.TokenUsed,
                StorageLimit= ps.StorageLimit,
                Status      = ps.Status,
                CreatedAt   = ps.CreatedAt,
            }).ToList(),
            RecentTransactions = recentTx,
            RecentPayments     = recentPayments,
        };

        await SafeSetCacheAsync(cacheKey, JsonSerializer.Serialize(dto), _detailOpts);

        return ServiceResult<PartnerDetailDto>.Ok(dto);
    }

    // ── UPDATE ────────────────────────────────────────────────
    public async Task<ServiceResult<PartnerDto>> UpdateAsync(Guid id, UpdatePartnerDto dto)
    {
        var partner = await db.Partners.FirstOrDefaultAsync(p => p.Id == id);
        if (partner is null)
            return ServiceResult<PartnerDto>.Fail("Partner not found");

        if (!string.IsNullOrWhiteSpace(dto.Name))
            partner.Name = dto.Name;

        if (!string.IsNullOrWhiteSpace(dto.Status))
            partner.Status = dto.Status.ToLower();

        if (!string.IsNullOrWhiteSpace(dto.Email))
            partner.Email = dto.Email;

        if (!string.IsNullOrWhiteSpace(dto.Phone))
            partner.Phone = dto.Phone;

        if (!string.IsNullOrWhiteSpace(dto.Description))
            partner.Description = dto.Description;

        if (!string.IsNullOrWhiteSpace(dto.Address))
            partner.Address = dto.Address;

        partner.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        await SafeRemoveCacheAsync($"partners:detail:{id}");

        return ServiceResult<PartnerDto>.Ok(new PartnerDto
        {
            Id          = partner.Id,
            Name        = partner.Name,
            Email       = partner.Email,
            Phone       = partner.Phone,
            Address     = partner.Address,
            Description = partner.Description,
            Status      = partner.Status,
            CreatedAt   = partner.CreatedAt,
            UpdatedAt   = partner.UpdatedAt,
        });
    }

    // ── DELETE ────────────────────────────────────────────────
    public async Task<ServiceResult<bool>> DeleteAsync(Guid id)
    {
        var partner = await db.Partners.FirstOrDefaultAsync(p => p.Id == id);
        if (partner is null)
            return ServiceResult<bool>.Fail("Partner not found");

        partner.Status    = "suspended";
        partner.UpdatedAt = DateTime.UtcNow;

        var services = await db.PartnerServices
            .Where(ps => ps.PartnerId == id && ps.Status == "active")
            .ToListAsync();

        services.ForEach(ps =>
        {
            ps.Status    = "paused";
            ps.UpdatedAt = DateTime.UtcNow;
        });

        await db.SaveChangesAsync();

        await SafeRemoveCacheAsync($"partners:detail:{id}");
        await SafeRemoveCacheAsync($"partners:tokens:{id}");

        return ServiceResult<bool>.Ok(true);
    }

    // ── GET TOKENS ────────────────────────────────────────────
    public async Task<ServiceResult<TokenInfoDto>> GetTokensAsync(Guid partnerId)
    {
        var cacheKey = $"partners:tokens:{partnerId}";

        var cached = await SafeGetCacheAsync(cacheKey);
        if (cached is not null)
            return ServiceResult<TokenInfoDto>.Ok(
                JsonSerializer.Deserialize<TokenInfoDto>(cached)!);

        var exists = await db.Partners.AnyAsync(p => p.Id == partnerId);
        if (!exists)
            return ServiceResult<TokenInfoDto>.Fail("Partner not found");

        var (balance, topup, used) = await CalcTokenBalance(partnerId);

        var transactions = await db.TokenTransactions
            .AsNoTracking()
            .Where(t => t.PartnerId == partnerId)
            .OrderByDescending(t => t.CreatedAt)
            .Take(20)
            .Select(t => new RecentTransactionDto
            {
                Id             = t.Id,
                TokensUsed     = t.TokensUsed,
                Cost           = t.Cost,
                ConversationId = t.ConversationId,
                GuestSessionId = t.GuestSessionId,
                CreatedAt      = t.CreatedAt,
            })
            .ToListAsync();

        var payments = await db.Payments
            .AsNoTracking()
            .Where(p => p.PartnerId == partnerId)
            .OrderByDescending(p => p.CreatedAt)
            .Take(20)
            .Select(p => new PaymentSummaryDto
            {
                Id            = p.Id,
                Amount        = p.Amount,
                PaymentMethod = p.PaymentMethod,
                Status        = p.Status,
                TokenAmount   = p.TokenAmount,
                Note          = p.Note,
                CreatedAt     = p.CreatedAt,
            })
            .ToListAsync();

        var dto = new TokenInfoDto
        {
            Balance      = balance,
            TotalTopup   = topup,
            TotalUsed    = used,
            Transactions = transactions,
            Payments     = payments,
        };

        await SafeSetCacheAsync(cacheKey, JsonSerializer.Serialize(dto), _tokenOpts);

        return ServiceResult<TokenInfoDto>.Ok(dto);
    }

    // ── ADJUST TOKEN ──────────────────────────────────────────
    public async Task<ServiceResult<int>> AdjustTokenAsync(Guid partnerId, AdjustTokenRequestDto dto)
    {
        var partner = await db.Partners.FirstOrDefaultAsync(p => p.Id == partnerId);
        if (partner is null)
            return ServiceResult<int>.Fail("Partner not found");

        if (dto.Amount < 0)
        {
            var (currentBalance, _, _) = await CalcTokenBalance(partnerId);
            if (Math.Abs(dto.Amount) > currentBalance)
                return ServiceResult<int>.Fail(
                    $"Không đủ token. Balance hiện tại: {currentBalance}");
        }

        db.Payments.Add(new Payment
        {
            Id            = Guid.NewGuid(),
            PartnerId     = partnerId,
            Amount        = 0,
            PaymentMethod = "grant",
            Status        = "completed",
            TokenAmount   = dto.Amount,
            Note          = dto.Reason,
            CreatedAt     = DateTime.UtcNow,
            UpdatedAt     = DateTime.UtcNow,
        });

        await db.SaveChangesAsync();

        // Xóa token cache và detail cache sau khi adjust
        await SafeRemoveCacheAsync($"partners:tokens:{partnerId}");
        await SafeRemoveCacheAsync($"partners:detail:{partnerId}");

        var (newBalance, _, _) = await CalcTokenBalance(partnerId);
        return ServiceResult<int>.Ok(newBalance);
    }

    // ── HELPER: CalcTokenBalance ──────────────────────────────
    private async Task<(int balance, int topup, int used)> CalcTokenBalance(Guid partnerId)
    {
        var topup = await db.Payments
            .Where(p => p.PartnerId == partnerId && p.Status == "completed")
            .SumAsync(p => p.TokenAmount ?? 0);

        var used = await db.TokenTransactions
            .Where(t => t.PartnerId == partnerId)
            .SumAsync(t => t.TokensUsed);

        return (topup - used, topup, used);
    }

    // ── SAFE CACHE HELPERS ────────────────────────────────────
    private async Task<string?> SafeGetCacheAsync(string key)
    {
        try { return await cache.GetStringAsync(key); }
        catch { return null; }
    }

    private async Task SafeSetCacheAsync(string key, string value,
        DistributedCacheEntryOptions opts)
    {
        try { await cache.SetStringAsync(key, value, opts); }
        catch { }
    }

    private async Task SafeRemoveCacheAsync(string key)
    {
        try { await cache.RemoveAsync(key); }
        catch { }
    }
}