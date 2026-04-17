using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AdminService.Application.Common;
using platform_ai_backend_netcore.Application.Partners.DTOs;

namespace platform_ai_backend_netcore.Application.Organization.Interfaces
{
    public interface IOrganizationService
    {
        Task<ServiceResult<PartnerDto>> GetByPartnerIdAsync(Guid partnerId);
        Task<ServiceResult<PartnerDto>> UpdateAsync(Guid partnerId, UpdatePartnerDto dto);
    }
}