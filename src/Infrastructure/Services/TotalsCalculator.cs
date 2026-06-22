using Domain.Entities;
using Domain.Enums;

namespace Infrastructure.Services;

internal static class TotalsCalculator
{
    public static decimal MonthlyCost(Subscription subscription) => subscription.BillingFrequency switch
    {
        BillingFrequency.Weekly => subscription.Cost * 52m / 12m,
        BillingFrequency.Monthly => subscription.Cost,
        BillingFrequency.Quarterly => subscription.Cost / 3m,
        BillingFrequency.Yearly => subscription.Cost / 12m,
        _ => subscription.Cost
    };

    public static decimal YearlyCost(Subscription subscription) => MonthlyCost(subscription) * 12m;

    public static bool RenewsWithin(DateOnly renewalDate, DateOnly today, int days)
    {
        var next = NextRenewalOnOrAfter(renewalDate, today);
        return next.DayNumber - today.DayNumber <= days;
    }

    public static DateOnly NextRenewalOnOrAfter(DateOnly renewalDate, DateOnly today)
    {
        var next = renewalDate;
        while (next < today)
        {
            next = next.AddYears(1);
        }

        return next;
    }
}
