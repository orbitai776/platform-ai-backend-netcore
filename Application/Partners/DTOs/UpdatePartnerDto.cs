using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace platform_ai_backend_netcore.Application.Partners.DTOs
{
    public class UpdatePartnerDto
    {
        [RegularExpression("^(pending|active|suspended)$",
        ErrorMessage = "Status phải là: pending | active | suspended")]
        public string? Status { get; set; }

        [MaxLength(255)]
        public string? Name { get; set; }

        [EmailAddress]
        public string? Email { get; set; }

        [MaxLength(50)]
        public string? Phone { get; set; }

        public string? Description { get; set; }
        public string? Address { get; set; }
    }
}