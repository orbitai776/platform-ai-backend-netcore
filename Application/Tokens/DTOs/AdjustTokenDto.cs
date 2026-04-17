using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace platform_ai_backend_netcore.Application.Partners.DTOs
{
    public class AdjustTokenRequestDto
    {
        [Required(ErrorMessage = "Amount là bắt buộc")]
        [Range(-1000000, 1000000, ErrorMessage = "Amount phải từ -1,000,000 đến 1,000,000")]
        public int Amount { get; set; }     // dương = cộng, âm = trừ

        [Required(ErrorMessage = "Reason là bắt buộc")]
        [MinLength(5, ErrorMessage = "Reason tối thiểu 5 ký tự")]
        [MaxLength(500)]
        public string Reason { get; set; } = string.Empty;
    }
}