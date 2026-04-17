using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace platform_ai_backend_netcore.Application.Tokens.DTOs
{
    public class TokenStatsDto
    {
        public Guid PartnerId { get; set; }
        public TokenBalanceDto Summary { get; set; } = null!;
        public List<TokenMonthlyDto> Monthly { get; set; } = [];
    }
}