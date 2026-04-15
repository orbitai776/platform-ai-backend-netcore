using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace platform_ai_backend_netcore.Application.Partners.DTOs
{
    public class QueryPartnerDto
    {
        public int Page { get; set; } = 1;
        public int Limit { get; set; } = 20;
        public string? Status { get; set; }   // pending | active | suspended
        public string? Search { get; set; }   // name hoặc email
    }
}