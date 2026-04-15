// AdminService.Application/Users/DTOs/UpdateUserDto.cs
using System.ComponentModel.DataAnnotations;

namespace AdminService.Application.Users.DTOs;

public class UpdateUserDto
{
    [RegularExpression("^(active|suspended|deleted)$",
        ErrorMessage = "Status phải là: active | suspended | deleted")]
    public string? Status { get; set; }

    [MaxLength(255)]
    public string? FullName { get; set; }
}