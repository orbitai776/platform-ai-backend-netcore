
using AdminService.Application.Users.DTOs;

namespace platform_ai_backend_netcore.Application.Users.DTOs
{
    public class UserDetailDto : UserDto
    {
        public string FirebaseUid { get; set; } = string.Empty;
        public List<UserSessionDto> ActiveSessions { get; set; } = [];
    }
}