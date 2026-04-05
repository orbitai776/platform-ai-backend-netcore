// AdminService.API/Controllers/PartnersController.cs
using AdminService.Application.Partners;
using Microsoft.AspNetCore.Mvc;
using platform_ai_backend_netcore.Application.Partners.DTOs;

namespace AdminService.API.Controllers;

[ApiController]
[Route("admin/partners")]
[Produces("application/json")]
public class PartnersController(IPartnerService partnerService) : ControllerBase
{
    /// <summary>Lấy danh sách partners — có filter status + search</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] QueryPartnerDto query)
    {
        if (query.Limit > 100) query.Limit = 100;
        return Ok(await partnerService.GetAllAsync(query));
    }

    /// <summary>Lấy chi tiết partner + services + token balance + transactions</summary>
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

    /// <summary>Xem token balance + lịch sử giao dịch của partner</summary>
    [HttpGet("{id:guid}/tokens")]
    public async Task<IActionResult> GetTokens(Guid id)
    {
        var result = await partnerService.GetTokensAsync(id);
        return result.Success
            ? Ok(result.Data)
            : NotFound(new { message = result.Message });
    }

    /// <summary>Điều chỉnh token thủ công — amount dương=cộng, âm=trừ</summary>
    [HttpPost("{id:guid}/tokens/adjust")]
    public async Task<IActionResult> AdjustToken(Guid id, [FromBody] AdjustTokenDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        if (dto.Amount == 0)
            return BadRequest(new { message = "Amount không được bằng 0" });

        var result = await partnerService.AdjustTokenAsync(id, dto);
        return result.Success
            ? Ok(new { newBalance = result.Data, adjusted = dto.Amount })
            : BadRequest(new { message = result.Message });
    }
}