using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AdminService.Application.Common;
using AdminService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using platform_ai_backend_netcore.Application.Partners.DTOs;
using platform_ai_backend_netcore.Application.Tokens.DTOs;
using platform_ai_backend_netcore.Application.Tokens.Interfaces;
using platform_ai_backend_netcore.Infrastructure.Data;

namespace platform_ai_backend_netcore.Infrastructure.Services
{
    public class TokenService(AppDbContext db) : ITokenService
    {

        // ── Balance ────────────────────────────────────────────────
        public async Task<ServiceResult<TokenBalanceDto>> GetBalanceAsync(Guid partnerId)
        {
            var exists = await db.Partners.AnyAsync(p => p.Id == partnerId);
            if (!exists)
                return ServiceResult<TokenBalanceDto>.Fail("Partner not found");

            var (balance, topup, used) = await CalcBalanceAsync(partnerId);

            return ServiceResult<TokenBalanceDto>.Ok(new TokenBalanceDto
            {
                PartnerId = partnerId,
                Balance = balance,
                TotalTopup = topup,
                TotalUsed = used,
            });
        }

        // ── Stats theo tháng ───────────────────────────────────────
        public async Task<ServiceResult<TokenStatsDto>> GetStatsAsync(Guid partnerId)
        {
            var exists = await db.Partners.AnyAsync(p => p.Id == partnerId);
            if (!exists)
                return ServiceResult<TokenStatsDto>.Fail("Partner not found");

            var (balance, topup, used) = await CalcBalanceAsync(partnerId);

            // Lấy 12 tháng gần nhất
            var fromDate = DateTime.UtcNow.AddMonths(-11);
            var firstOfMonth = new DateTime(fromDate.Year, fromDate.Month, 1, 0, 0, 0, DateTimeKind.Utc);

            // Topup theo tháng
            var topupByMonth = await db.Payments
                .AsNoTracking()
                .Where(p => p.PartnerId == partnerId
                         && p.Status == "completed"
                         && p.CreatedAt >= firstOfMonth)
                .GroupBy(p => new { p.CreatedAt.Year, p.CreatedAt.Month })
                .Select(g => new
                {
                    g.Key.Year,
                    g.Key.Month,
                    Total = g.Sum(p => p.TokenAmount ?? 0),
                })
                .ToListAsync();

            // Used theo tháng
            var usedByMonth = await db.TokenTransactions
                .AsNoTracking()
                .Where(t => t.PartnerId == partnerId && t.CreatedAt >= firstOfMonth)
                .GroupBy(t => new { t.CreatedAt.Year, t.CreatedAt.Month })
                .Select(g => new
                {
                    g.Key.Year,
                    g.Key.Month,
                    Total = g.Sum(t => t.TokensUsed),
                })
                .ToListAsync();

            // Ghép 12 tháng
            var monthly = Enumerable.Range(0, 12)
                .Select(i => DateTime.UtcNow.AddMonths(-11 + i))
                .Select(d =>
                {
                    var t = topupByMonth.FirstOrDefault(x => x.Year == d.Year && x.Month == d.Month)?.Total ?? 0;
                    var u = usedByMonth.FirstOrDefault(x => x.Year == d.Year && x.Month == d.Month)?.Total ?? 0;
                    return new TokenMonthlyDto
                    {
                        Month = $"{d.Year}-{d.Month:D2}",
                        Topup = t,
                        Used = u,
                        Net = t - u,
                    };
                })
                .ToList();

            return ServiceResult<TokenStatsDto>.Ok(new TokenStatsDto
            {
                PartnerId = partnerId,
                Summary = new TokenBalanceDto
                {
                    PartnerId = partnerId,
                    Balance = balance,
                    TotalTopup = topup,
                    TotalUsed = used,
                },
                Monthly = monthly,
            });
        }

        // ── Payments (lịch sử nạp) ─────────────────────────────────
        public async Task<PagedResult<TokenPaymentDto>> GetPaymentsAsync(Guid partnerId, TokenHistoryQueryDto query)
        {
            var q = db.Payments
                .AsNoTracking()
                .Where(p => p.PartnerId == partnerId);

            if (!string.IsNullOrWhiteSpace(query.Status))
                q = q.Where(p => p.Status == query.Status.ToLower());

            if (DateTime.TryParse(query.From, out var from))
                q = q.Where(p => p.CreatedAt >= from.ToUniversalTime());

            if (DateTime.TryParse(query.To, out var to))
                q = q.Where(p => p.CreatedAt <= to.ToUniversalTime().AddDays(1));

            var total = await q.CountAsync();

            var data = await q
                .OrderByDescending(p => p.CreatedAt)
                .Skip((query.Page - 1) * query.Limit)
                .Take(query.Limit)
                .Select(p => new TokenPaymentDto
                {
                    Id = p.Id,
                    Amount = p.Amount,
                    PaymentMethod = p.PaymentMethod,
                    Status = p.Status,
                    TransactionId = p.TransactionId,
                    TokenAmount = p.TokenAmount,
                    Note = p.Note,
                    CreatedAt = p.CreatedAt,
                })
                .ToListAsync();

            return new PagedResult<TokenPaymentDto>
            {
                Data = data,
                Total = total,
                Page = query.Page,
                Limit = query.Limit,
            };
        }

        // ── Transactions (lịch sử tiêu) ────────────────────────────
        public async Task<PagedResult<TokenTransactionDto>> GetTransactionsAsync(Guid partnerId, TokenHistoryQueryDto query)
        {
            var q = db.TokenTransactions
                .AsNoTracking()
                .Where(t => t.PartnerId == partnerId);

            if (DateTime.TryParse(query.From, out var from))
                q = q.Where(t => t.CreatedAt >= from.ToUniversalTime());

            if (DateTime.TryParse(query.To, out var to))
                q = q.Where(t => t.CreatedAt <= to.ToUniversalTime().AddDays(1));

            var total = await q.CountAsync();

            var data = await q
                .OrderByDescending(t => t.CreatedAt)
                .Skip((query.Page - 1) * query.Limit)
                .Take(query.Limit)
                .Select(t => new TokenTransactionDto
                {
                    Id = t.Id,
                    ServiceId = t.Id,
                    ConversationId = t.ConversationId,
                    GuestSessionId = t.GuestSessionId,
                    UserId = t.UserId,
                    TokensUsed = t.TokensUsed,
                    Cost = t.Cost,
                    CreatedAt = t.CreatedAt,
                })
                .ToListAsync();

            return new PagedResult<TokenTransactionDto>
            {
                Data = data,
                Total = total,
                Page = query.Page,
                Limit = query.Limit,
            };
        }

        // ── Adjust token ───────────────────────────────────────────
        public async Task<ServiceResult<AdjustTokenResponseDto>> AdjustAsync(Guid partnerId, AdjustTokenRequestDto dto)
        {
            var partner = await db.Partners.FirstOrDefaultAsync(p => p.Id == partnerId);
            if (partner is null)
                return ServiceResult<AdjustTokenResponseDto>.Fail("Partner not found");

            if (dto.Amount == 0)
                return ServiceResult<AdjustTokenResponseDto>.Fail("Amount không được bằng 0");

            if (dto.Amount < 0)
            {
                var (currentBalance, _, _) = await CalcBalanceAsync(partnerId);
                if (Math.Abs(dto.Amount) > currentBalance)
                    return ServiceResult<AdjustTokenResponseDto>.Fail(
                        $"Không đủ token. Balance hiện tại: {currentBalance}");
            }

            db.Payments.Add(new Payment
            {
                Id = Guid.NewGuid(),
                PartnerId = partnerId,
                Amount = 0,
                PaymentMethod = "grant",
                Status = "completed",
                TokenAmount = dto.Amount,
                Note = dto.Reason,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });

            await db.SaveChangesAsync();

            var (newBalance, _, _) = await CalcBalanceAsync(partnerId);

            return ServiceResult<AdjustTokenResponseDto>.Ok(new AdjustTokenResponseDto
            {
                NewBalance = newBalance,
                Adjusted = dto.Amount,
            });
        }

        // ── Private helper ─────────────────────────────────────────
        private async Task<(int balance, int topup, int used)> CalcBalanceAsync(Guid partnerId)
        {
            var topup = await db.Payments
                .Where(p => p.PartnerId == partnerId && p.Status == "completed")
                .SumAsync(p => p.TokenAmount ?? 0);

            var used = await db.TokenTransactions
                .Where(t => t.PartnerId == partnerId)
                .SumAsync(t => t.TokensUsed);

            return (topup - used, topup, used);
        }
    }
}