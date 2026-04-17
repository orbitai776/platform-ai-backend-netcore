using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Threading.Tasks;
using AdminService.Application.Common;
using AdminService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using platform_ai_backend_netcore.Application.Organization.DTOs;
using platform_ai_backend_netcore.Application.Organization.Interfaces;
using platform_ai_backend_netcore.Application.Partners.DTOs;
using platform_ai_backend_netcore.Infrastructure.Data;

namespace platform_ai_backend_netcore.Infrastructure.Services
{
    public class OrganizationService(AppDbContext db) : IOrganizationService
    {
        public async Task<ServiceResult<PartnerDto>> GetByPartnerIdAsync(Guid partnerId)
        {
            var partner = await db.Partners
                .AsNoTracking()
                .Include(p => p.OwnerUser)
                .FirstOrDefaultAsync(p => p.Id == partnerId);
            if (partner is null) return ServiceResult<PartnerDto>.Fail("Partner not found");
            return ServiceResult<PartnerDto>.Ok(MapToDto(partner));
        }

        public async Task<ServiceResult<PartnerDto>> UpdateAsync(Guid partnerId, UpdatePartnerDto dto)
        {
            var partner = await db.Partners
            .Include(p => p.OwnerUser)
            .FirstOrDefaultAsync(p => p.Id == partnerId);

            if (partner is null)
                return ServiceResult<PartnerDto>.Fail("Partner not found");

            if (!string.IsNullOrWhiteSpace(dto.Name)) partner.Name = dto.Name;
            if (!string.IsNullOrWhiteSpace(dto.Email)) partner.Email = dto.Email;
            if (!string.IsNullOrWhiteSpace(dto.Phone)) partner.Phone = dto.Phone;
            if (!string.IsNullOrWhiteSpace(dto.Address)) partner.Address = dto.Address;
            if (!string.IsNullOrWhiteSpace(dto.Description)) partner.Description = dto.Description;
            if (!string.IsNullOrWhiteSpace(dto.Status)) partner.Status = dto.Status;

            partner.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();

            return ServiceResult<PartnerDto>.Ok(MapToDto(partner));
        }
        private static PartnerDto MapToDto(AdminService.Domain.Entities.Partner p) => new()
        {
            Id = p.Id,
            Name = p.Name,
            Email = p.Email,
            Phone = p.Phone,
            Address = p.Address,
            Description = p.Description,
            Status = p.Status,
            OwnerEmail = p.OwnerUser?.Email,
            OwnerName = p.OwnerUser?.FullName,
            CreatedAt = p.CreatedAt,
            UpdatedAt = p.UpdatedAt,
        };
    }
}