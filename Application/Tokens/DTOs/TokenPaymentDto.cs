using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace platform_ai_backend_netcore.Application.Tokens.DTOs
{
    public class TokenPaymentDto
    {
        public Guid Id { get; set; }
        public decimal Amount { get; set; }
        public string? PaymentMethod { get; set; }
        public string Status { get; set; } = "";
        public string? TransactionId { get; set; }
        public int? TokenAmount { get; set; }
        public string? Note { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}