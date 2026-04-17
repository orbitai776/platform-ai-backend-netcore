using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace platform_ai_backend_netcore.Application.Tokens.DTOs
{
    public class TokenHistoryQueryDto
    {
        public int Page { get; set; } = 1;
        public int Limit { get; set; } = 20;
        public string? Status { get; set; }       // dùng cho payments: pending|completed|failed
        public string? From { get; set; }       // ISO date string "2024-01-01"
        public string? To { get; set; }       // ISO date string "2024-12-31"
    }
}