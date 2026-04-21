// API/Controllers/OrganizationController.cs
using Microsoft.AspNetCore.Mvc;
using platform_ai_backend_netcore.Application.Organization.Interfaces;
using platform_ai_backend_netcore.Application.Partners.DTOs;

namespace platform_ai_backend_netcore.API.Controllers;

/// <summary>
/// Quản lý thông tin doanh nghiệp (organization profile).
/// Chỉ trả về thông tin cơ bản của tổ chức — không bao gồm services, token, transactions.
/// Dùng /v1/api/admin/partners/{id} để lấy full detail.
/// </summary>
[ApiController]
[Route("v1/api/admin/organizations")]
[Produces("application/json")]
public class OrganizationController(IOrganizationService organizationService) : ControllerBase
{
    /// <summary>Danh sách tổ chức — có filter status + search, paging</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] QueryPartnerDto query)
    {
        if (query.Limit > 100) query.Limit = 100;
        return Ok(await organizationService.GetAllAsync(query));
    }

    /// <summary>Chi tiết tổ chức theo partnerId</summary>
    [HttpGet("{partnerId:guid}")]
    public async Task<IActionResult> Get(Guid partnerId)
    {
        var result = await organizationService.GetByPartnerIdAsync(partnerId);
        return result.Success
            ? Ok(result.Data)
            : NotFound(new { message = result.Message });
    }

    /// <summary>Cập nhật thông tin tổ chức — chỉ update field được gửi lên</summary>
    [HttpPatch("{partnerId:guid}")]
    public async Task<IActionResult> Update(Guid partnerId, [FromBody] UpdatePartnerDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var result = await organizationService.UpdateAsync(partnerId, dto);
        return result.Success
            ? Ok(result.Data)
            : NotFound(new { message = result.Message });
    }
}