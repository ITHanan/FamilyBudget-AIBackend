using Application.DTOs;
using Application.Interfaces;
using Domain.Entities;

namespace Infrastructure.Services;

public sealed class FinancialHealthService : IFinancialHealthService
{
    public FinancialHealthDto Calculate(IReadOnlyList<Subscription> subscriptions, DateOnly today)
    {
        var totalMonthlyCost = subscriptions.Sum(TotalsCalculator.MonthlyCost);
        var totalYearlyCost = subscriptions.Sum(TotalsCalculator.YearlyCost);
        var categoryGroups = subscriptions
            .GroupBy(x => x.Category.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToList();
        var hasDuplicateCategory = categoryGroups.Any(x => x.Count() >= 3);
        var hasUpcomingRenewal = subscriptions.Any(x => TotalsCalculator.RenewsWithin(x.RenewalDate, today, 7));

        var score = 100;

        if (totalMonthlyCost > 1000m)
        {
            score -= 15;
        }

        if (subscriptions.Count > 10)
        {
            score -= 10;
        }

        if (hasDuplicateCategory)
        {
            score -= 10;
        }

        if (hasUpcomingRenewal)
        {
            score -= 5;
        }

        if (totalYearlyCost > 12000m)
        {
            score -= 15;
        }

        score = Math.Clamp(score, 0, 100);

        return new FinancialHealthDto
        {
            Score = score,
            Status = GetStatus(score),
            PotentialMonthlySavings = CalculatePotentialMonthlySavings(categoryGroups),
            Recommendation = GetRecommendation(categoryGroups, totalMonthlyCost, hasUpcomingRenewal)
        };
    }

    private static decimal CalculatePotentialMonthlySavings(IEnumerable<IGrouping<string, Subscription>> categoryGroups)
    {
        var savings = categoryGroups
            .Where(x => x.Count() >= 3)
            .Sum(x => x.Min(TotalsCalculator.MonthlyCost));

        return Math.Round(savings, 2);
    }

    private static string GetRecommendation(
        IEnumerable<IGrouping<string, Subscription>> categoryGroups,
        decimal totalMonthlyCost,
        bool hasUpcomingRenewal)
    {
        if (categoryGroups.Any(x => x.Key.Equals("streaming", StringComparison.OrdinalIgnoreCase) && x.Count() >= 3))
        {
            return "Review your streaming subscriptions. You may be paying for overlapping services.";
        }

        if (totalMonthlyCost > 1000m)
        {
            return "Your subscription costs are high. Review your largest monthly subscriptions.";
        }

        if (hasUpcomingRenewal)
        {
            return "You have subscriptions renewing soon. Check if you still need them before they renew.";
        }

        return "Your subscriptions look healthy. Keep reviewing them monthly.";
    }

    private static string GetStatus(int score) => score switch
    {
        >= 80 => "Excellent",
        >= 60 => "Good",
        >= 40 => "Needs Attention",
        _ => "High Risk"
    };
}
