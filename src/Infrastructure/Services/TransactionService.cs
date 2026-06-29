using Application.DTOs;
using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services;

public sealed class TransactionService(AppDbContext dbContext) : ITransactionService
{
    public async Task<BankTransactionDto?> UpdateCategoryAsync(int userId, int transactionId, UpdateTransactionCategoryRequest request, CancellationToken cancellationToken)
    {
        var transaction = await dbContext.BankTransactions
            .SingleOrDefaultAsync(x => x.Id == transactionId && x.UserId == userId, cancellationToken);

        if (transaction is null)
        {
            return null;
        }

        transaction.Category = request.Category.Trim();
        transaction.NeedsReview = false;

        if (request.RememberRule)
        {
            var matchText = BuildRuleMatchText(transaction.NormalizedDescription);
            if (!string.IsNullOrWhiteSpace(matchText))
            {
                var existingRule = await dbContext.TransactionCategoryRules.SingleOrDefaultAsync(
                    x => x.UserId == userId && x.MatchText == matchText,
                    cancellationToken);

                if (existingRule is null)
                {
                    dbContext.TransactionCategoryRules.Add(new TransactionCategoryRule
                    {
                        UserId = userId,
                        MatchText = matchText,
                        Category = transaction.Category,
                        Priority = 200
                    });
                }
                else
                {
                    existingRule.Category = transaction.Category;
                    existingRule.Priority = Math.Max(existingRule.Priority, 200);
                }

                var matchingTransactions = await dbContext.BankTransactions
                    .Where(x => x.UserId == userId && x.NormalizedDescription.Contains(matchText))
                    .ToListAsync(cancellationToken);

                foreach (var matchingTransaction in matchingTransactions)
                {
                    matchingTransaction.Category = transaction.Category;
                    matchingTransaction.NeedsReview = false;
                }
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return ToDto(transaction);
    }

    public async Task<TransactionSummaryDto> GetSummaryAsync(int userId, DateOnly? from, DateOnly? to, CancellationToken cancellationToken)
    {
        var query = dbContext.BankTransactions.Where(x => x.UserId == userId);
        if (from.HasValue)
        {
            query = query.Where(x => x.TransactionDate >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(x => x.TransactionDate <= to.Value);
        }

        var transactions = await query.ToListAsync(cancellationToken);
        var income = Math.Round(transactions.Where(x => x.Amount > 0).Sum(x => x.Amount), 2);
        var expenses = Math.Round(Math.Abs(transactions.Where(x => x.Amount < 0 && !x.IsInternalTransfer).Sum(x => x.Amount)), 2);
        var netSavings = Math.Round(income - expenses, 2);
        var savingsRate = income > 0 ? Math.Round(netSavings / income * 100, 2) : 0;

        var categoryTotals = transactions
            .Where(x => x.Amount < 0 && !x.IsInternalTransfer)
            .GroupBy(x => x.Category)
            .Select(x => new CategoryTotalDto(x.Key, Math.Round(Math.Abs(x.Sum(t => t.Amount)), 2), x.Count()))
            .OrderByDescending(x => x.Amount)
            .ToList();

        var largestTransactions = transactions
            .Where(x => x.Amount < 0 && !x.IsInternalTransfer)
            .OrderBy(x => x.Amount)
            .Take(5)
            .Select(ToDto)
            .ToList();

        var recurringPaymentTotal = Math.Round(Math.Abs(transactions
            .Where(x => x.IsRecurringCandidate && x.Amount < 0)
            .Sum(x => x.Amount)), 2);

        return new TransactionSummaryDto(income, expenses, netSavings, savingsRate, categoryTotals, largestTransactions, recurringPaymentTotal);
    }

    public async Task<IReadOnlyList<RecurringPaymentCandidateDto>> GetRecurringCandidatesAsync(int userId, CancellationToken cancellationToken)
    {
        var transactions = await dbContext.BankTransactions
            .Where(x => x.UserId == userId && x.Amount < 0 && !x.IsInternalTransfer)
            .ToListAsync(cancellationToken);

        return transactions
            .GroupBy(x => new { x.NormalizedDescription, Amount = Math.Round(Math.Abs(x.Amount), 0), x.Category })
            .Where(x => x.Count() >= 2)
            .Select(x => new RecurringPaymentCandidateDto(
                ToMerchantName(x.Key.NormalizedDescription),
                Math.Round(x.Average(t => Math.Abs(t.Amount)), 2),
                DetectFrequency(x.Select(t => t.TransactionDate).Order().ToList()),
                Math.Clamp(55 + x.Count() * 10, 0, 95),
                x.Min(t => t.TransactionDate),
                x.Max(t => t.TransactionDate),
                x.Key.Category,
                x.Count()))
            .OrderByDescending(x => x.Confidence)
            .ThenByDescending(x => x.AverageAmount)
            .ToList();
    }

    internal static BankTransactionDto ToDto(BankTransaction transaction) => new(
        transaction.Id,
        transaction.BankStatementId,
        transaction.TransactionDate,
        transaction.Description,
        transaction.Amount,
        transaction.Balance,
        transaction.Currency,
        transaction.Category,
        transaction.RawText,
        transaction.IsIncome,
        transaction.IsInternalTransfer,
        transaction.IsRecurringCandidate,
        transaction.NeedsReview,
        transaction.CreatedAt);

    private static string BuildRuleMatchText(string normalizedDescription)
    {
        var words = normalizedDescription
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => x.Length > 2)
            .Take(2);

        return string.Join(' ', words).Trim();
    }

    private static string ToMerchantName(string normalizedDescription)
    {
        var text = BuildRuleMatchText(normalizedDescription);
        return string.IsNullOrWhiteSpace(text) ? normalizedDescription : text;
    }

    private static string DetectFrequency(IReadOnlyList<DateOnly> dates)
    {
        if (dates.Count < 2)
        {
            return "Unknown";
        }

        var averageDays = dates.Zip(dates.Skip(1), (a, b) => b.DayNumber - a.DayNumber).Average();
        return averageDays switch
        {
            >= 340 and <= 390 => "Yearly",
            >= 80 and <= 105 => "Quarterly",
            >= 25 and <= 35 => "Monthly",
            >= 6 and <= 9 => "Weekly",
            _ => "Recurring"
        };
    }
}
