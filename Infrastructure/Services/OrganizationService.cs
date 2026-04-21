// Infrastructure/Services/OrganizationService.cs
using AdminService.Application.Common;
using AdminService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using platform_ai_backend_netcore.Application.Organization.DTOs;
using platform_ai_backend_netcore.Application.Organization.Interfaces;
using platform_ai_backend_netcore.Application.Partners.DTOs;
using platform_ai_backend_netcore.Infrastructure.Cache;
using platform_ai_backend_netcore.Infrastructure.Data;

namespace platform_ai_backend_netcore.Infrastructure.Services;

/// <summary>
/// Quản lý thông tin doanh nghiệp (organization profile).
/// Chỉ đọc/ghi bảng <c>partners</c> — không touch token, services, transactions.
/// </summary>
public class OrganizationService(
    AppDbContext                 db,
    ICacheService                cache,
    ILogger<OrganizationService> logger) : IOrganizationService
{
    private static readonly TimeSpan _listTtl   = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan _detailTtl = TimeSpan.FromMinutes(10);

    // ── GET ALL ────────────────────────────────────────────────
    public async Task<PagedResult<OrganizationDto>> GetAllAsync(QueryPartnerDto query)
    {
        var key    = CacheKeys.OrgList(query.Page, query.Limit,
                                       query.Status ?? "", query.Search ?? "");
        var cached = await cache.GetAsync<PagedResult<OrganizationDto>>(key);
        if (cached is not null)
        {
            logger.LogDebug("[OrgService] GetAll cache hit | page={Page}", query.Page);
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

        var total = await q.CountAsync();
        var data  = await q
            .OrderByDescending(p => p.CreatedAt)
            .Skip((query.Page - 1) * query.Limit)
            .Take(query.Limit)
            .Select(p => new OrganizationDto
            {
                Id          = p.Id,
                Name        = p.Name,
                Email       = p.Email,
                Phone       = p.Phone,
                Address     = p.Address,
                Description = p.Description,
                Status      = p.Status,
                OwnerEmail  = p.OwnerUser != null ? p.OwnerUser.Email    : null,
                OwnerName   = p.OwnerUser != null ? p.OwnerUser.FullName : null,
                CreatedAt   = p.CreatedAt,
                UpdatedAt   = p.UpdatedAt,
            })
            .ToListAsync();

        var result = new PagedResult<OrganizationDto>
        {
            Data  = data,
            Total = total,
            Page  = query.Page,
            Limit = query.Limit,
        };

        await cache.SetAsync(key, result, _listTtl);

        logger.LogInformation(
            "[OrgService] GetAll | total={Total} page={Page} limit={Limit}",
            total, query.Page, query.Limit);

        return result;
    }

    // ── GET BY PARTNER ID ──────────────────────────────────────
    public async Task<ServiceResult<OrganizationDto>> GetByPartnerIdAsync(Guid partnerId)
    {
        var key    = CacheKeys.OrgDetail(partnerId);
        var cached = await cache.GetAsync<OrganizationDto>(key);
        if (cached is not null)
        {
            logger.LogDebug("[OrgService] GetByPartnerId cache hit | partnerId={Id}", partnerId);
            return ServiceResult<OrganizationDto>.Ok(cached);
        }

        var partner = await db.Partners
            .AsNoTracking()
            .Include(p => p.OwnerUser)
            .FirstOrDefaultAsync(p => p.Id == partnerId);

        if (partner is null)
        {
            logger.LogWarning("[OrgService] GetByPartnerId not found | partnerId={Id}", partnerId);
            return ServiceResult<OrganizationDto>.Fail("Partner not found");
        }

        var dto = MapToDto(partner);
        await cache.SetAsync(key, dto, _detailTtl);

        logger.LogInformation(
            "[OrgService] GetByPartnerId | partnerId={Id} name={Name} status={Status}",
            partner.Id, partner.Name, partner.Status);

        return ServiceResult<OrganizationDto>.Ok(dto);
    }

    // ── UPDATE ─────────────────────────────────────────────────
    public async Task<ServiceResult<OrganizationDto>> UpdateAsync(Guid partnerId, UpdatePartnerDto dto)
    {
        var partner = await db.Partners
            .Include(p => p.OwnerUser)
            .FirstOrDefaultAsync(p => p.Id == partnerId);

        if (partner is null)
        {
            logger.LogWarning("[OrgService] Update not found | partnerId={Id}", partnerId);
            return ServiceResult<OrganizationDto>.Fail("Partner not found");
        }

        if (!string.IsNullOrWhiteSpace(dto.Name))        partner.Name        = dto.Name;
        if (!string.IsNullOrWhiteSpace(dto.Email))       partner.Email       = dto.Email;
        if (!string.IsNullOrWhiteSpace(dto.Phone))       partner.Phone       = dto.Phone;
        if (!string.IsNullOrWhiteSpace(dto.Address))     partner.Address     = dto.Address;
        if (!string.IsNullOrWhiteSpace(dto.Description)) partner.Description = dto.Description;
        if (!string.IsNullOrWhiteSpace(dto.Status))      partner.Status      = dto.Status;

        partner.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        // Invalidate org cache
        await cache.RemoveAsync(CacheKeys.OrgDetail(partnerId));
        await cache.RemoveByPrefixAsync(CacheKeys.OrgListPrefix);

        // Invalidate partner cache — cùng data nguồn
        await cache.RemoveAsync(CacheKeys.PartnerDetail(partnerId));
        await cache.RemoveByPrefixAsync(CacheKeys.PartnerListPrefix);

        logger.LogInformation(
            "[OrgService] Update | partnerId={Id} name={Name} status={Status}",
            partnerId, partner.Name, partner.Status);

        return ServiceResult<OrganizationDto>.Ok(MapToDto(partner));
    }

    // ── HELPER ─────────────────────────────────────────────────
    private static OrganizationDto MapToDto(Partner p) => new()
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
    };
}