// AdminService.API/Controllers/UsersController.cs
using AdminService.Application.Users;
using AdminService.Application.Users.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace AdminService.API.Controllers;

[ApiController]
[Route("v1/api/admin/users")] 
[Produces("application/json")]
public class UsersController(IUserService userService) : ControllerBase
{
    /// <summary>Lấy danh sách users — có filter status + search</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] QueryUserDto query)
    {
        if (query.Limit > 100) query.Limit = 100;
        return Ok(await userService.GetAllAsync(query));
    }

    /// <summary>Lấy chi tiết user + active sessions</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await userService.GetByIdAsync(id);
        return result.Success
            ? Ok(result.Data)
            : NotFound(new { message = result.Message });
    }

    /// <summary>Cập nhật status hoặc full_name của user</summary>
    [HttpPatch("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateUserDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var result = await userService.UpdateAsync(id, dto);
        return result.Success
            ? Ok(result.Data)
            : NotFound(new { message = result.Message });
    }

    /// <summary>Soft delete user — chuyển status=deleted + revoke sessions</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var result = await userService.DeleteAsync(id);
        return result.Success
            ? Ok(new { success = true, message = "User đã bị xoá" })
            : NotFound(new { message = result.Message });
    }
}