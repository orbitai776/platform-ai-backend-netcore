// Infrastructure/Services/OrganizationService.cs
using AdminService.Application.Common;
using AdminService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using platform_ai_backend_netcore.Application.Organization.Interfaces;
using platform_ai_backend_netcore.Application.Partners.DTOs;
using platform_ai_backend_netcore.Infrastructure.Cache;
using platform_ai_backend_netcore.Infrastructure.Data;

namespace platform_ai_backend_netcore.Infrastructure.Services;

public class OrganizationService(
    AppDbContext                    db,
    ICacheService                   cache,
    ILogger<OrganizationService>    logger) : IOrganizationService
{
    // Cache strategy:
    //   detail → 10 phút — org info ít thay đổi, chỉ partner tự update
    private static readonly TimeSpan _detailTtl = TimeSpan.FromMinutes(10);

    // ── GET BY PARTNER ID ──────────────────────────────────────
    public async Task<ServiceResult<PartnerDto>> GetByPartnerIdAsync(Guid partnerId)
    {
        var key    = CacheKeys.OrgDetail(partnerId);
        var cached = await cache.GetAsync<PartnerDto>(key);
        if (cached is not null)
        {
            logger.LogDebug("[OrgService] GetByPartnerId cache hit | partnerId={PartnerId}", partnerId);
            return ServiceResult<PartnerDto>.Ok(cached);
        }

        var partner = await db.Partners
            .AsNoTracking()
            .Include(p => p.OwnerUser)
            .FirstOrDefaultAsync(p => p.Id == partnerId);

        if (partner is null)
        {
            logger.LogWarning("[OrgService] GetByPartnerId not found | partnerId={PartnerId}", partnerId);
            return ServiceResult<PartnerDto>.Fail("Partner not found");
        }

        var dto = MapToDto(partner);
        await cache.SetAsync(key, dto, _detailTtl);

        logger.LogInformation(
            "[OrgService] GetByPartnerId | partnerId={PartnerId} name={Name} status={Status}",
            partner.Id, partner.Name, partner.Status);

        return ServiceResult<PartnerDto>.Ok(dto);
    }

    // ── UPDATE ─────────────────────────────────────────────────
    public async Task<ServiceResult<PartnerDto>> UpdateAsync(Guid partnerId, UpdatePartnerDto dto)
    {
        var partner = await db.Partners
            .Include(p => p.OwnerUser)
            .FirstOrDefaultAsync(p => p.Id == partnerId);

        if (partner is null)
        {
            logger.LogWarning("[OrgService] Update not found | partnerId={PartnerId}", partnerId);
            return ServiceResult<PartnerDto>.Fail("Partner not found");
        }

        if (!string.IsNullOrWhiteSpace(dto.Name))        partner.Name        = dto.Name;
        if (!string.IsNullOrWhiteSpace(dto.Email))       partner.Email       = dto.Email;
        if (!string.IsNullOrWhiteSpace(dto.Phone))       partner.Phone       = dto.Phone;
        if (!string.IsNullOrWhiteSpace(dto.Address))     partner.Address     = dto.Address;
        if (!string.IsNullOrWhiteSpace(dto.Description)) partner.Description = dto.Description;
        if (!string.IsNullOrWhiteSpace(dto.Status))      partner.Status      = dto.Status;

        partner.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        await cache.RemoveAsync(CacheKeys.OrgDetail(partnerId));

        logger.LogInformation(
            "[OrgService] Update | partnerId={PartnerId} name={Name}",
            partnerId, partner.Name);

        return ServiceResult<PartnerDto>.Ok(MapToDto(partner));
    }

    // ── HELPER ─────────────────────────────────────────────────
    private static PartnerDto MapToDto(Partner p) => new()
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