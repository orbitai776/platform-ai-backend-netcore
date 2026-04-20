// Infrastructure/Services/PartnerService.cs
using AdminService.Application.Common;
using AdminService.Application.Partners;
using AdminService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using platform_ai_backend_netcore.Application.Partners.DTOs;
using platform_ai_backend_netcore.Infrastructure.Cache;
using platform_ai_backend_netcore.Infrastructure.Data;

namespace platform_ai_backend_netcore.Infrastructure.Services;

public class PartnerService(
    AppDbContext               db,
    ICacheService              cache,
    ILogger<PartnerService>    logger) : IPartnerService
{
    private static readonly TimeSpan _listTtl   = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan _detailTtl = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan _tokenTtl  = TimeSpan.FromMinutes(2);

    // ── GET ALL ────────────────────────────────────────────────
    public async Task<PagedResult<PartnerDto>> GetAllAsync(QueryPartnerDto query)
    {
        var key    = CacheKeys.PartnerList(query.Page, query.Limit,
                                           query.Status ?? "", query.Search ?? "");
        var cached = await cache.GetAsync<PagedResult<PartnerDto>>(key);
        if (cached is not null)
        {
            logger.LogDebug("[PartnerService] GetAll served from cache | page={Page}", query.Page);
            return cached;
        }

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

        // FIX: EF Core DbContext không thread-safe — chạy tuần tự thay vì Task.WhenAll
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

        await cache.SetAsync(key, result, _listTtl);

        logger.LogInformation(
            "[PartnerService] GetAll | total={Total} page={Page} limit={Limit}",
            result.Total, query.Page, query.Limit);

        return result;
    }

    // ── GET BY ID ──────────────────────────────────────────────
    public async Task<ServiceResult<PartnerDetailDto>> GetByIdAsync(Guid id)
    {
        var key    = CacheKeys.PartnerDetail(id);
        var cached = await cache.GetAsync<PartnerDetailDto>(key);
        if (cached is not null)
        {
            logger.LogDebug("[PartnerService] GetById cache hit | partnerId={PartnerId}", id);
            return ServiceResult<PartnerDetailDto>.Ok(cached);
        }

        // FIX: chạy tuần tự — DbContext không hỗ trợ concurrent queries
        var partner = await db.Partners
            .AsNoTracking()
            .AsSplitQuery()
            .Include(p => p.OwnerUser)
            .Include(p => p.PartnerServices).ThenInclude(ps => ps.Service)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (partner is null)
        {
            logger.LogWarning("[PartnerService] GetById not found | partnerId={PartnerId}", id);
            return ServiceResult<PartnerDetailDto>.Fail("Partner not found");
        }

        var transactions = await db.TokenTransactions
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

        var payments = await db.Payments
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

        var topup = await db.Payments
            .Where(p => p.PartnerId == id && p.Status == "completed")
            .SumAsync(p => p.TokenAmount ?? 0);

        var used    = await db.TokenTransactions
            .Where(t => t.PartnerId == id)
            .SumAsync(t => t.TokensUsed);

        var balance = topup - used;

        var dto = new PartnerDetailDto
        {
            Id                 = partner.Id,
            Name               = partner.Name,
            Email              = partner.Email,
            Phone              = partner.Phone,
            Address            = partner.Address,
            Description        = partner.Description,
            Status             = partner.Status,
            OwnerEmail         = partner.OwnerUser?.Email,
            OwnerName          = partner.OwnerUser?.FullName,
            TokenBalance       = balance,
            TotalTopup         = topup,
            TotalUsed          = used,
            CreatedAt          = partner.CreatedAt,
            UpdatedAt          = partner.UpdatedAt,
            Services           = partner.PartnerServices.Select(ps => new PartnerServiceSummaryDto
            {
                Id           = ps.Id,
                ServiceName  = ps.Service?.Name ?? "",
                ServiceType  = ps.Service?.Type ?? "",
                CustomName   = ps.Name,
                TokenLimit   = ps.TokenLimit,
                TokenUsed    = ps.TokenUsed,
                StorageLimit = ps.StorageLimit,
                Status       = ps.Status,
                CreatedAt    = ps.CreatedAt,
            }).ToList(),
            RecentTransactions = transactions,
            RecentPayments     = payments,
        };

        await cache.SetAsync(key, dto, _detailTtl);

        logger.LogInformation(
            "[PartnerService] GetById | partnerId={PartnerId} name={Name} balance={Balance} services={ServiceCount}",
            partner.Id, partner.Name, balance, dto.Services.Count);

        return ServiceResult<PartnerDetailDto>.Ok(dto);
    }

    // ── UPDATE ─────────────────────────────────────────────────
    public async Task<ServiceResult<PartnerDto>> UpdateAsync(Guid id, UpdatePartnerDto dto)
    {
        var partner = await db.Partners.FirstOrDefaultAsync(p => p.Id == id);
        if (partner is null)
        {
            logger.LogWarning("[PartnerService] Update not found | partnerId={PartnerId}", id);
            return ServiceResult<PartnerDto>.Fail("Partner not found");
        }

        var oldStatus = partner.Status;
        if (!string.IsNullOrWhiteSpace(dto.Name))        partner.Name        = dto.Name;
        if (!string.IsNullOrWhiteSpace(dto.Email))       partner.Email       = dto.Email;
        if (!string.IsNullOrWhiteSpace(dto.Phone))       partner.Phone       = dto.Phone;
        if (!string.IsNullOrWhiteSpace(dto.Address))     partner.Address     = dto.Address;
        if (!string.IsNullOrWhiteSpace(dto.Description)) partner.Description = dto.Description;
        if (!string.IsNullOrWhiteSpace(dto.Status))      partner.Status      = dto.Status;

        partner.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        await cache.RemoveAsync(CacheKeys.PartnerDetail(id));
        await cache.RemoveByPrefixAsync(CacheKeys.PartnerListPrefix);

        logger.LogInformation(
            "[PartnerService] Update | partnerId={PartnerId} oldStatus={OldStatus} newStatus={NewStatus}",
            id, oldStatus, partner.Status);

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

    // ── DELETE ─────────────────────────────────────────────────
    public async Task<ServiceResult<bool>> DeleteAsync(Guid id)
    {
        var partner = await db.Partners.FirstOrDefaultAsync(p => p.Id == id);
        if (partner is null)
        {
            logger.LogWarning("[PartnerService] Delete not found | partnerId={PartnerId}", id);
            return ServiceResult<bool>.Fail("Partner not found");
        }

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

        await cache.RemoveAsync(
            CacheKeys.PartnerDetail(id),
            CacheKeys.PartnerTokens(id));
        await cache.RemoveByPrefixAsync(CacheKeys.PartnerListPrefix);

        logger.LogInformation(
            "[PartnerService] Delete (soft) | partnerId={PartnerId} pausedServices={Count}",
            id, services.Count);

        return ServiceResult<bool>.Ok(true);
    }

    // ── GET TOKENS ─────────────────────────────────────────────
    public async Task<ServiceResult<TokenInfoDto>> GetTokensAsync(Guid partnerId)
    {
        var key    = CacheKeys.PartnerTokens(partnerId);
        var cached = await cache.GetAsync<TokenInfoDto>(key);
        if (cached is not null)
        {
            logger.LogDebug("[PartnerService] GetTokens cache hit | partnerId={PartnerId}", partnerId);
            return ServiceResult<TokenInfoDto>.Ok(cached);
        }

        var exists = await db.Partners.AnyAsync(p => p.Id == partnerId);
        if (!exists)
        {
            logger.LogWarning("[PartnerService] GetTokens partner not found | partnerId={PartnerId}", partnerId);
            return ServiceResult<TokenInfoDto>.Fail("Partner not found");
        }

        // FIX: chạy tuần tự
        var topup = await db.Payments
            .Where(p => p.PartnerId == partnerId && p.Status == "completed")
            .SumAsync(p => p.TokenAmount ?? 0);

        var used = await db.TokenTransactions
            .Where(t => t.PartnerId == partnerId)
            .SumAsync(t => t.TokensUsed);

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

        var balance = topup - used;

        var dto = new TokenInfoDto
        {
            Balance      = balance,
            TotalTopup   = topup,
            TotalUsed    = used,
            Transactions = transactions,
            Payments     = payments,
        };

        await cache.SetAsync(key, dto, _tokenTtl);

        logger.LogInformation(
            "[PartnerService] GetTokens | partnerId={PartnerId} balance={Balance} topup={Topup} used={Used}",
            partnerId, balance, topup, used);

        return ServiceResult<TokenInfoDto>.Ok(dto);
    }

    // ── ADJUST TOKEN ───────────────────────────────────────────
    public async Task<ServiceResult<int>> AdjustTokenAsync(Guid partnerId, AdjustTokenRequestDto dto)
    {
        var partner = await db.Partners.FirstOrDefaultAsync(p => p.Id == partnerId);
        if (partner is null)
        {
            logger.LogWarning("[PartnerService] AdjustToken partner not found | partnerId={PartnerId}", partnerId);
            return ServiceResult<int>.Fail("Partner not found");
        }

        if (dto.Amount < 0)
        {
            var topup = await db.Payments
                .Where(p => p.PartnerId == partnerId && p.Status == "completed")
                .SumAsync(p => p.TokenAmount ?? 0);

            var used    = await db.TokenTransactions
                .Where(t => t.PartnerId == partnerId)
                .SumAsync(t => t.TokensUsed);

            var balance = topup - used;
            if (Math.Abs(dto.Amount) > balance)
            {
                logger.LogWarning(
                    "[PartnerService] AdjustToken insufficient | partnerId={PartnerId} requested={Amount} balance={Balance}",
                    partnerId, dto.Amount, balance);
                return ServiceResult<int>.Fail($"Không đủ token. Balance hiện tại: {balance}");
            }
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

        await cache.RemoveAsync(
            CacheKeys.PartnerTokens(partnerId),
            CacheKeys.PartnerDetail(partnerId));

        var newTopup = await db.Payments
            .Where(p => p.PartnerId == partnerId && p.Status == "completed")
            .SumAsync(p => p.TokenAmount ?? 0);

        var newUsed = await db.TokenTransactions
            .Where(t => t.PartnerId == partnerId)
            .SumAsync(t => t.TokensUsed);

        var newBalance = newTopup - newUsed;

        logger.LogInformation(
            "[PartnerService] AdjustToken | partnerId={PartnerId} delta={Delta} reason={Reason} newBalance={NewBalance}",
            partnerId, dto.Amount, dto.Reason, newBalance);

        return ServiceResult<int>.Ok(newBalance);
    }
}