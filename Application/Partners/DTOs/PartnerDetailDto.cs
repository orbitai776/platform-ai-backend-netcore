// Application/Partners/DTOs/PartnerDetailDto.cs
namespace platform_ai_backend_netcore.Application.Partners.DTOs;

public class PartnerDetailDto : PartnerDto
{
    // Token lấy từ billing_wallet — cached counter, không SUM
    public int WalletBalance  { get; set; }   // available_tokens
    public int WalletTotalUsed { get; set; }  // total_used

    public List<PartnerServiceSummaryDto> Services           { get; set; } = [];
    public List<RecentTransactionDto>     RecentTransactions { get; set; } = [];
    public List<PaymentSummaryDto>        RecentPayments     { get; set; } = [];
}