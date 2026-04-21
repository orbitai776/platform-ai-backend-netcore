// Application/Organization/Interfaces/IOrganizationService.cs
using AdminService.Application.Common;
using platform_ai_backend_netcore.Application.Organization.DTOs;
using platform_ai_backend_netcore.Application.Partners.DTOs;

namespace platform_ai_backend_netcore.Application.Organization.Interfaces;

public interface IOrganizationService
{
    Task<PagedResult<OrganizationDto>>   GetAllAsync(QueryPartnerDto query);
    Task<ServiceResult<OrganizationDto>> GetByPartnerIdAsync(Guid partnerId);
    Task<ServiceResult<OrganizationDto>> UpdateAsync(Guid partnerId, UpdatePartnerDto dto);
}