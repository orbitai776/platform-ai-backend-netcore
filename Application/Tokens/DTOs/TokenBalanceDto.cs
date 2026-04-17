using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace platform_ai_backend_netcore.Application.Tokens.DTOs
{
    //Summary balance token of partner
    public class TokenBalanceDto
    {
        public Guid PartnerId { get; set; }
        public int Balance { get; set; }
        public int TotalTopup { get; set; }
        public int TotalUsed { get; set; }
    }
}