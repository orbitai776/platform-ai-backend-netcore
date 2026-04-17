using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace platform_ai_backend_netcore.Application.Tokens.DTOs
{
    public class AdjustTokenResponseDto
    {
        public int NewBalance { get; set; }
        public int Adjusted { get; set; }
    }
}