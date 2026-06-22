using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Data;
using Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Jobs;

public sealed class NotificationReminderJob(AppDbContext dbContext) : INotificationReminderJob
{
    public async Task CreateRenewalRemindersAsync()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var subscriptions = await dbContext.Subscriptions.ToListAsync();

        foreach (var subscription in subscriptions.Where(x => TotalsCalculator.RenewsWithin(x.RenewalDate, today, 7)))
        {
            var nextRenewal = TotalsCalculator.NextRenewalOnOrAfter(subscription.RenewalDate, today);
            var message = $"{subscription.Name} renews on {nextRenewal:yyyy-MM-dd}.";

            var exists = await dbContext.Notifications.AnyAsync(x =>
                x.UserId == subscription.UserId &&
                x.SubscriptionId == subscription.Id &&
                x.Message == message &&
                x.CreatedAt.Date == DateTime.UtcNow.Date);

            if (!exists)
            {
                dbContext.Notifications.Add(new Notification
                {
                    UserId = subscription.UserId,
                    SubscriptionId = subscription.Id,
                    Message = message
                });
            }
        }

        await dbContext.SaveChangesAsync();
    }
}
