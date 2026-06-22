namespace Application.DTOs;

public sealed record DashboardSummaryDto(
    decimal TotalMonthlySubscriptionCost,
    decimal TotalYearlySubscriptionCost,
    int UpcomingRenewalsInNext7Days,
    int SubscriptionCount,
    IReadOnlyList<SubscriptionDto> UpcomingRenewals,
    FinancialHealthDto FinancialHealth);
