using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace platform_ai_backend_netcore.Application.Partners.DTOs
{
    public class RecentTransactionDto
    {
        public Guid Id { get; set; }
    public int TokensUsed { get; set; }
    public decimal Cost { get; set; }
    public string? ConversationId { get; set; }
    public string? GuestSessionId { get; set; }
    public DateTime CreatedAt { get; set; }
    }
}