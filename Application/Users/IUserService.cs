// AdminService.Application/Users/IUserService.cs
using AdminService.Application.Common;
using AdminService.Application.Users.DTOs;
using platform_ai_backend_netcore.Application.Users.DTOs;

namespace AdminService.Application.Users;

public interface IUserService
{
    Task<PagedResult<UserDto>> GetAllAsync(QueryUserDto query);
    Task<ServiceResult<UserDetailDto>> GetByIdAsync(Guid id);
    Task<ServiceResult<UserDto>> UpdateAsync(Guid id, UpdateUserDto dto);
    Task<ServiceResult<bool>> DeleteAsync(Guid id);
}