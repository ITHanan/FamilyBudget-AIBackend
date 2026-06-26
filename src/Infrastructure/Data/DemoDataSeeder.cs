using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.Data;

public static class DemoDataSeeder
{
    public static async Task SeedDemoDataAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        const string username = "demo";
        var existingUser = await dbContext.Users
            .Include(x => x.Subscriptions)
            .SingleOrDefaultAsync(x => x.Username == username, cancellationToken);

        if (existingUser is not null && existingUser.Subscriptions.Count > 0)
        {
            return;
        }

        var user = existingUser ?? new User
        {
            Username = username,
            FirstName = "Demo",
            LastName = "User",
            Email = "demo@familybudget.local",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("DemoPassword123!")
        };

        if (existingUser is null)
        {
            dbContext.Users.Add(user);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var subscriptions = new[]
        {
            new Subscription
            {
                UserId = user.Id,
                Name = "Netflix",
                Cost = 149m,
                BillingFrequency = BillingFrequency.Monthly,
                RenewalDate = today.AddDays(4),
                Category = "Entertainment"
            },
            new Subscription
            {
                UserId = user.Id,
                Name = "Spotify Family",
                Cost = 199m,
                BillingFrequency = BillingFrequency.Monthly,
                RenewalDate = today.AddDays(11),
                Category = "Music"
            },
            new Subscription
            {
                UserId = user.Id,
                Name = "Microsoft 365 Family",
                Cost = 999m,
                BillingFrequency = BillingFrequency.Yearly,
                RenewalDate = today.AddMonths(2),
                Category = "Productivity"
            }
        };

        dbContext.Subscriptions.AddRange(subscriptions);
        await dbContext.SaveChangesAsync(cancellationToken);

        dbContext.Notifications.Add(new Notification
        {
            UserId = user.Id,
            SubscriptionId = subscriptions[0].Id,
            Message = "Netflix renews soon.",
            IsRead = false
        });

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
