// API/Controllers/PartnersController.cs
using AdminService.Application.Partners;
using Microsoft.AspNetCore.Mvc;
using platform_ai_backend_netcore.Application.Partners.DTOs;

namespace AdminService.API.Controllers;

/// <summary>
/// Quản lý partner — full data bao gồm thông tin doanh nghiệp,
/// wallet balance, services, recent transactions và payments.
/// </summary>
[ApiController]
[Route("v1/api/admin/partners")]
[Produces("application/json")]
public class PartnersController(IPartnerService partnerService) : ControllerBase
{
    /// <summary>Danh sách partners — thông tin doanh nghiệp + wallet balance + services</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] QueryPartnerDto query)
    {
        if (query.Limit > 100) query.Limit = 100;
        return Ok(await partnerService.GetAllAsync(query));
    }

    /// <summary>Chi tiết partner — full info gồm services, wallet, recent transactions + payments</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await partnerService.GetByIdAsync(id);
        return result.Success
            ? Ok(result.Data)
            : NotFound(new { message = result.Message });
    }

    /// <summary>Cập nhật thông tin hoặc status partner</summary>
    [HttpPatch("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdatePartnerDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var result = await partnerService.UpdateAsync(id, dto);
        return result.Success
            ? Ok(result.Data)
            : NotFound(new { message = result.Message });
    }

    /// <summary>Soft delete partner — suspend + pause toàn bộ services</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var result = await partnerService.DeleteAsync(id);
        return result.Success
            ? Ok(new { success = true, message = "Partner đã bị suspend" })
            : NotFound(new { message = result.Message });
    }
}