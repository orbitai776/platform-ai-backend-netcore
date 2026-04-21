// Application/Organization/DTOs/OrganizationDto.cs
namespace platform_ai_backend_netcore.Application.Organization.DTOs;

using platform_ai_backend_netcore.Application.Partners.DTOs;

/// <summary>
/// Thông tin doanh nghiệp thuần túy — chỉ từ bảng partners.
/// Không bao gồm services, token, transactions.
/// </summary>
public class OrganizationDto : PartnerDto
{
    // Không thêm gì — PartnerDto đã đủ các field doanh nghiệp.
    // Class này tồn tại để tách biệt ngữ nghĩa Organization vs Partner.
}