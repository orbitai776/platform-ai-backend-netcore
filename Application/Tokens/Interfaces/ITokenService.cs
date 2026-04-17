using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AdminService.Application.Common;
using platform_ai_backend_netcore.Application.Partners.DTOs;
using platform_ai_backend_netcore.Application.Tokens.DTOs;

namespace platform_ai_backend_netcore.Application.Tokens.Interfaces
{
    public interface ITokenService
    {
        /// <summary>Balance hiện tại của partner</summary>
        Task<ServiceResult<TokenBalanceDto>> GetBalanceAsync(Guid partnerId);

        /// <summary>Thống kê token theo tháng (12 tháng gần nhất)</summary>
        Task<ServiceResult<TokenStatsDto>> GetStatsAsync(Guid partnerId);

        /// <summary>Lịch sử nạp token (payments) — có phân trang + filter</summary>
        Task<PagedResult<TokenPaymentDto>> GetPaymentsAsync(Guid partnerId, TokenHistoryQueryDto query);

        /// <summary>Lịch sử tiêu token (token_transactions) — có phân trang + filter</summary>
        Task<PagedResult<TokenTransactionDto>> GetTransactionsAsync(Guid partnerId, TokenHistoryQueryDto query);

        /// <summary>Điều chỉnh token thủ công</summary>
        Task<ServiceResult<AdjustTokenResponseDto>> AdjustAsync(Guid partnerId, AdjustTokenRequestDto dto);
    }
}