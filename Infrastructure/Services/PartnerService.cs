// Infrastructure/Services/PartnerService.cs
using AdminService.Application.Common;
using AdminService.Application.Partners;
using AdminService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using platform_ai_backend_netcore.Application.Partners.DTOs;
using platform_ai_backend_netcore.Infrastructure.Cache;
using platform_ai_backend_netcore.Infrastructure.Data;

namespace platform_ai_backend_netcore.Infrastructure.Services;

/// <summary>
/// Quản lý partner — full data bao gồm services, token balance (từ billing_wallet),
/// recent transactions và payments.
/// </summary>
public class PartnerService(
    AppDbContext             db,
    ICacheService            cache,
    ILogger<PartnerService>  logger) : IPartnerService
{
    private static readonly TimeSpan _listTtl   = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan _detailTtl = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan _tokenTtl  = TimeSpan.FromMinutes(2);

    // ── GET ALL ────────────────────────────────────────────────
    public async Task<PagedResult<PartnerDetailDto>> GetAllAsync(QueryPartnerDto query)
    {
        var key    = CacheKeys.PartnerList(query.Page, query.Limit,
                                           query.Status ?? "", query.Search ?? "");
        var cached = await cache.GetAsync<PagedResult<PartnerDetailDto>>(key);
        if (cached is not null)
        {
            logger.LogDebug("[PartnerService] GetAll cache hit | page={Page}", query.Page);
            return cached;
        }

        var q = db.Partners
            .AsNoTracking()
            .AsSplitQuery()
            .Include(p => p.OwnerUser)
            .Include(p => p.PartnerServices).ThenInclude(ps => ps.Service)
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

        // FIX: EF Core DbContext không thread-safe — chạy tuần tự
        var total    = await q.CountAsync();
        var partners = await q
            .OrderByDescending(p => p.CreatedAt)
            .Skip((query.Page - 1) * query.Limit)
            .Take(query.Limit)
            .ToListAsync();

        // ✅ Batch query billing_wallet — 1 query cho tất cả partners (thay vì N SUM queries)
        var partnerIds = partners.Select(p => p.Id).ToList();

        var wallets = await db.BillingWallets
            .AsNoTracking()
            .Where(w => partnerIds.Contains(w.PartnerId))
            .ToDictionaryAsync(w => w.PartnerId);

        var data = partners.Select(p =>
        {
            var wallet = wallets.GetValueOrDefault(p.Id);
            return new PartnerDetailDto
            {
                Id          = p.Id,
                Name        = p.Name,
                Email       = p.Email,
                Phone       = p.Phone,
                Address     = p.Address,
                Description = p.Description,
                Status      = p.Status,
                OwnerEmail  = p.OwnerUser?.Email,
                OwnerName   = p.OwnerUser?.FullName,
                CreatedAt   = p.CreatedAt,
                UpdatedAt   = p.UpdatedAt,

                // Token từ billing_wallet
                WalletBalance   = wallet?.AvailableTokens ?? 0,
                WalletTotalUsed = wallet?.TotalUsed       ?? 0,

                // Services — không load recent tx/payments ở list view để tránh N+1
                Services           = p.PartnerServices.Select(ps => new PartnerServiceSummaryDto
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
                RecentTransactions = [],
                RecentPayments     = [],
            };
        }).ToList();

        var result = new PagedResult<PartnerDetailDto>
        {
            Data  = data,
            Total = total,
            Page  = query.Page,
            Limit = query.Limit,
        };

        await cache.SetAsync(key, result, _listTtl);

        logger.LogInformation(
            "[PartnerService] GetAll | total={Total} page={Page} limit={Limit}",
            total, query.Page, query.Limit);

        return result;
    }

    // ── GET BY ID ──────────────────────────────────────────────
    public async Task<ServiceResult<PartnerDetailDto>> GetByIdAsync(Guid id)
    {
        var key    = CacheKeys.PartnerDetail(id);
        var cached = await cache.GetAsync<PartnerDetailDto>(key);
        if (cached is not null)
        {
            logger.LogDebug("[PartnerService] GetById cache hit | partnerId={Id}", id);
            return ServiceResult<PartnerDetailDto>.Ok(cached);
        }

        // FIX: AsSplitQuery tránh cartesian explosion khi include nhiều collection
        var partner = await db.Partners
            .AsNoTracking()
            .AsSplitQuery()
            .Include(p => p.OwnerUser)
            .Include(p => p.PartnerServices).ThenInclude(ps => ps.Service)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (partner is null)
        {
            logger.LogWarning("[PartnerService] GetById not found | partnerId={Id}", id);
            return ServiceResult<PartnerDetailDto>.Fail("Partner not found");
        }

        // ✅ Dùng billing_wallet thay vì SUM — O(1) thay vì O(n)
        var wallet = await db.BillingWallets
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.PartnerId == id);

        // Lịch sử gần nhất — chạy tuần tự (DbContext không thread-safe)
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

        var dto = new PartnerDetailDto
        {
            Id          = partner.Id,
            Name        = partner.Name,
            Email       = partner.Email,
            Phone       = partner.Phone,
            Address     = partner.Address,
            Description = partner.Description,
            Status      = partner.Status,
            OwnerEmail  = partner.OwnerUser?.Email,
            OwnerName   = partner.OwnerUser?.FullName,
            CreatedAt   = partner.CreatedAt,
            UpdatedAt   = partner.UpdatedAt,

            // Token từ billing_wallet
            WalletBalance   = wallet?.AvailableTokens ?? 0,
            WalletTotalUsed = wallet?.TotalUsed       ?? 0,

            Services = partner.PartnerServices.Select(ps => new PartnerServiceSummaryDto
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
            "[PartnerService] GetById | partnerId={Id} name={Name} walletBalance={Balance} services={Count}",
            partner.Id, partner.Name, dto.WalletBalance, dto.Services.Count);

        return ServiceResult<PartnerDetailDto>.Ok(dto);
    }

    // ── UPDATE ─────────────────────────────────────────────────
    public async Task<ServiceResult<PartnerDto>> UpdateAsync(Guid id, UpdatePartnerDto dto)
    {
        var partner = await db.Partners.FirstOrDefaultAsync(p => p.Id == id);
        if (partner is null)
        {
            logger.LogWarning("[PartnerService] Update not found | partnerId={Id}", id);
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

        // Invalidate org cache — cùng data nguồn
        await cache.RemoveAsync(CacheKeys.OrgDetail(id));
        await cache.RemoveByPrefixAsync(CacheKeys.OrgListPrefix);

        logger.LogInformation(
            "[PartnerService] Update | partnerId={Id} oldStatus={Old} newStatus={New}",
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
            logger.LogWarning("[PartnerService] Delete not found | partnerId={Id}", id);
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

        await cache.RemoveAsync(CacheKeys.PartnerDetail(id), CacheKeys.PartnerTokens(id));
        await cache.RemoveByPrefixAsync(CacheKeys.PartnerListPrefix);
        await cache.RemoveAsync(CacheKeys.OrgDetail(id));
        await cache.RemoveByPrefixAsync(CacheKeys.OrgListPrefix);

        logger.LogInformation(
            "[PartnerService] Delete (soft) | partnerId={Id} pausedServices={Count}",
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
            logger.LogDebug("[PartnerService] GetTokens cache hit | partnerId={Id}", partnerId);
            return ServiceResult<TokenInfoDto>.Ok(cached);
        }

        var exists = await db.Partners.AnyAsync(p => p.Id == partnerId);
        if (!exists)
        {
            logger.LogWarning("[PartnerService] GetTokens not found | partnerId={Id}", partnerId);
            return ServiceResult<TokenInfoDto>.Fail("Partner not found");
        }

        // ✅ Dùng billing_wallet
        var wallet = await db.BillingWallets
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.PartnerId == partnerId);

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
            Balance      = wallet?.AvailableTokens ?? 0,
            TotalTopup   = 0,   // billing_wallet không lưu total_topup riêng
            TotalUsed    = wallet?.TotalUsed ?? 0,
            Transactions = transactions,
            Payments     = payments,
        };

        await cache.SetAsync(key, dto, _tokenTtl);

        logger.LogInformation(
            "[PartnerService] GetTokens | partnerId={Id} balance={Balance} totalUsed={Used}",
            partnerId, dto.Balance, dto.TotalUsed);

        return ServiceResult<TokenInfoDto>.Ok(dto);
    }

    // ── ADJUST TOKEN ───────────────────────────────────────────
    public async Task<ServiceResult<int>> AdjustTokenAsync(Guid partnerId, AdjustTokenRequestDto dto)
    {
        var partner = await db.Partners.FirstOrDefaultAsync(p => p.Id == partnerId);
        if (partner is null)
        {
            logger.LogWarning("[PartnerService] AdjustToken not found | partnerId={Id}", partnerId);
            return ServiceResult<int>.Fail("Partner not found");
        }

        if (dto.Amount < 0)
        {
            var wallet = await db.BillingWallets
                .AsNoTracking()
                .FirstOrDefaultAsync(w => w.PartnerId == partnerId);

            var currentBalance = wallet?.AvailableTokens ?? 0;
            if (Math.Abs(dto.Amount) > currentBalance)
            {
                logger.LogWarning(
                    "[PartnerService] AdjustToken insufficient | partnerId={Id} requested={Amount} balance={Balance}",
                    partnerId, dto.Amount, currentBalance);
                return ServiceResult<int>.Fail($"Không đủ token. Balance hiện tại: {currentBalance}");
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

        await cache.RemoveAsync(CacheKeys.PartnerTokens(partnerId), CacheKeys.PartnerDetail(partnerId));

        // Đọc lại từ billing_wallet sau khi Django service cập nhật
        var updatedWallet = await db.BillingWallets
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.PartnerId == partnerId);

        var newBalance = updatedWallet?.AvailableTokens ?? 0;

        logger.LogInformation(
            "[PartnerService] AdjustToken | partnerId={Id} delta={Delta} reason={Reason} newBalance={Balance}",
            partnerId, dto.Amount, dto.Reason, newBalance);

        return ServiceResult<int>.Ok(newBalance);
    }
}