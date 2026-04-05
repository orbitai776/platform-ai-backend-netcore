// AdminService.Application/Users/DTOs/QueryUserDto.cs
namespace AdminService.Application.Users.DTOs;

public class QueryUserDto
{
    public int Page { get; set; } = 1;
    public int Limit { get; set; } = 20;
    public string? Status { get; set; }   // active | suspended | deleted
    public string? Search { get; set; }   // email hoặc full_name
}