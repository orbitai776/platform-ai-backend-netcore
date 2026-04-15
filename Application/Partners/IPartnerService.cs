// AdminService.Application/Partners/IPartnerService.cs
using AdminService.Application.Common;
using platform_ai_backend_netcore.Application.Partners.DTOs;

namespace AdminService.Application.Partners;

public interface IPartnerService
{
    Task<PagedResult<PartnerDto>> GetAllAsync(QueryPartnerDto query);
    Task<ServiceResult<PartnerDetailDto>> GetByIdAsync(Guid id);
    Task<ServiceResult<PartnerDto>> UpdateAsync(Guid id, UpdatePartnerDto dto);
    Task<ServiceResult<bool>> DeleteAsync(Guid id);
    Task<ServiceResult<TokenInfoDto>> GetTokensAsync(Guid partnerId);
    Task<ServiceResult<int>> AdjustTokenAsync(Guid partnerId, AdjustTokenDto dto);
}