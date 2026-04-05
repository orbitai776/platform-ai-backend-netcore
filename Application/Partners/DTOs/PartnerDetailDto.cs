using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace platform_ai_backend_netcore.Application.Partners.DTOs
{
    public class PartnerDetailDto : PartnerDto
    {
        public int TokenBalance { get; set; }
        public int TotalTopup { get; set; }
        public int TotalUsed { get; set; }
        public List<PartnerServiceSummaryDto> Services { get; set; } = [];
        public List<RecentTransactionDto> RecentTransactions { get; set; } = [];
        public List<PaymentSummaryDto> RecentPayments { get; set; } = [];
    }
}