using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AdminService.Domain.Entities;

namespace platform_ai_backend_netcore.Domain.Entities
{
    public class BillingWallet
    {
        public Guid Id { get; set; }
        public Guid PartnerId { get; set; }
        /// <summary>billing_wallet.available_tokens — token còn lại</summary>
        public int AvailableTokens { get; set; }
        /// <summary>billing_wallet.total_used — tổng token đã dùng</summary>
        public int TotalUsed { get; set; }
        public DateTime UpdatedAt { get; set; }

        // Navigation
        public Partner? Partner { get; set; }
    }
}