// Application/Partners/IPartnerService.cs
using AdminService.Application.Common;
using platform_ai_backend_netcore.Application.Partners.DTOs;

namespace AdminService.Application.Partners;

public interface IPartnerService
{
    /// <summary>Danh sách partners — thông tin doanh nghiệp + wallet balance + services, có filter + paging.</summary>
    Task<PagedResult<PartnerDetailDto>> GetAllAsync(QueryPartnerDto query);

    /// <summary>Chi tiết partner — full info bao gồm services, wallet, recent transactions + payments.</summary>
    Task<ServiceResult<PartnerDetailDto>> GetByIdAsync(Guid id);

    /// <summary>Cập nhật thông tin partner.</summary>
    Task<ServiceResult<PartnerDto>> UpdateAsync(Guid id, UpdatePartnerDto dto);

    /// <summary>Soft delete partner — suspend + pause toàn bộ services.</summary>
    Task<ServiceResult<bool>> DeleteAsync(Guid id);

    /// <summary>Lấy token info (balance + recent transactions + payments).</summary>
    Task<ServiceResult<TokenInfoDto>> GetTokensAsync(Guid partnerId);

    /// <summary>Điều chỉnh token thủ công.</summary>
    Task<ServiceResult<int>> AdjustTokenAsync(Guid partnerId, AdjustTokenRequestDto dto);
}