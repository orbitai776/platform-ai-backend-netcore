using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace platform_ai_backend_netcore.Application.Tokens.DTOs
{
    public class TokenMonthlyDto
    {
        public string Month { get; set; } = ""; // "2024-01"
        public int Topup { get; set; }
        public int Used { get; set; }
        public int Net { get; set; }        // Topup - Used
    }
}