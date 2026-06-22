using Application.DTOs;
using Application.Interfaces;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services;

public sealed class DashboardService(AppDbContext dbContext, IFinancialHealthService financialHealthService) : IDashboardService
{
    public async Task<DashboardSummaryDto> GetSummaryAsync(int userId, CancellationToken cancellationToken)
    {
        var subscriptions = await dbContext.Subscriptions
            .Where(x => x.UserId == userId)
            .OrderBy(x => x.RenewalDate)
            .ToListAsync(cancellationToken);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var upcoming = subscriptions
            .Where(x => TotalsCalculator.RenewsWithin(x.RenewalDate, today, 7))
            .OrderBy(x => TotalsCalculator.NextRenewalOnOrAfter(x.RenewalDate, today))
            .Select(SubscriptionService.ToDto)
            .ToList();

        return new DashboardSummaryDto(
            Math.Round(subscriptions.Sum(TotalsCalculator.MonthlyCost), 2),
            Math.Round(subscriptions.Sum(TotalsCalculator.YearlyCost), 2),
            upcoming.Count,
            subscriptions.Count,
            upcoming,
            financialHealthService.Calculate(subscriptions, today));
    }
}
