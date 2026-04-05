using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace platform_ai_backend_netcore.Application.Partners.DTOs
{
    public class TokenInfoDto
    {
        public int Balance { get; set; }
        public int TotalTopup { get; set; }
        public int TotalUsed { get; set; }
        public List<RecentTransactionDto> Transactions { get; set; } = [];
        public List<PaymentSummaryDto> Payments { get; set; } = [];
    }
}