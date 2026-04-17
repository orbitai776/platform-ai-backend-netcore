// API/Controllers/TokenController.cs
using Microsoft.AspNetCore.Mvc;
using platform_ai_backend_netcore.Application.Partners.DTOs;
using platform_ai_backend_netcore.Application.Tokens.DTOs;
using platform_ai_backend_netcore.Application.Tokens.Interfaces;

namespace AdminService.API.Controllers;

[ApiController]
[Route("v1/api/admin/partners/{partnerId:guid}/tokens")]
[Produces("application/json")]
public class TokenController(ITokenService tokenService) : ControllerBase
{
    /// <summary>Xem balance token hiện tại của partner</summary>
    [HttpGet("balance")]
    public async Task<IActionResult> GetBalance(Guid partnerId)
    {
        var result = await tokenService.GetBalanceAsync(partnerId);
        return result.Success
            ? Ok(result.Data)
            : NotFound(new { message = result.Message });
    }

    /// <summary>Thống kê token theo tháng (12 tháng gần nhất)</summary>
    [HttpGet("stats")]
    public async Task<IActionResult> GetStats(Guid partnerId)
    {
        var result = await tokenService.GetStatsAsync(partnerId);
        return result.Success
            ? Ok(result.Data)
            : NotFound(new { message = result.Message });
    }

    /// <summary>Lịch sử nạp token — có phân trang, filter status + date range</summary>
    [HttpGet("payments")]
    public async Task<IActionResult> GetPayments(
        Guid partnerId,
        [FromQuery] TokenHistoryQueryDto query)
    {
        if (query.Limit > 100) query.Limit = 100;
        return Ok(await tokenService.GetPaymentsAsync(partnerId, query));
    }

    /// <summary>Lịch sử tiêu token — có phân trang, filter date range</summary>
    [HttpGet("transactions")]
    public async Task<IActionResult> GetTransactions(
        Guid partnerId,
        [FromQuery] TokenHistoryQueryDto query)
    {
        if (query.Limit > 100) query.Limit = 100;
        return Ok(await tokenService.GetTransactionsAsync(partnerId, query));
    }

    /// <summary>Điều chỉnh token thủ công — amount dương=cộng, âm=trừ</summary>
    [HttpPost("adjust")]
    public async Task<IActionResult> Adjust(
        Guid partnerId,
        [FromBody] AdjustTokenRequestDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var result = await tokenService.AdjustAsync(partnerId, dto);
        return result.Success
            ? Ok(result.Data)
            : BadRequest(new { message = result.Message });
    }
}