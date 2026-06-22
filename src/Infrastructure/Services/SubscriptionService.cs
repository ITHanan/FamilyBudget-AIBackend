using Application.DTOs;
using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services;

public sealed class SubscriptionService(AppDbContext dbContext) : ISubscriptionService
{
    public async Task<IReadOnlyList<SubscriptionDto>> GetAllAsync(int userId, CancellationToken cancellationToken)
    {
        return await dbContext.Subscriptions
            .Where(x => x.UserId == userId)
            .OrderBy(x => x.RenewalDate)
            .Select(x => new SubscriptionDto(x.Id, x.Name, x.Cost, x.BillingFrequency, x.RenewalDate, x.Category, x.CreatedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<SubscriptionDto?> GetByIdAsync(int userId, int id, CancellationToken cancellationToken)
    {
        var subscription = await dbContext.Subscriptions
            .SingleOrDefaultAsync(x => x.Id == id && x.UserId == userId, cancellationToken);

        return subscription is null ? null : ToDto(subscription);
    }

    public async Task<SubscriptionDto> CreateAsync(int userId, SubscriptionRequest request, CancellationToken cancellationToken)
    {
        Validate(request);

        var subscription = new Subscription
        {
            UserId = userId,
            Name = request.Name.Trim(),
            Cost = request.Cost,
            BillingFrequency = request.BillingFrequency,
            RenewalDate = request.RenewalDate,
            Category = request.Category.Trim()
        };

        dbContext.Subscriptions.Add(subscription);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ToDto(subscription);
    }

    public async Task<SubscriptionDto?> UpdateAsync(int userId, int id, SubscriptionRequest request, CancellationToken cancellationToken)
    {
        Validate(request);

        var subscription = await dbContext.Subscriptions
            .SingleOrDefaultAsync(x => x.Id == id && x.UserId == userId, cancellationToken);

        if (subscription is null)
        {
            return null;
        }

        subscription.Name = request.Name.Trim();
        subscription.Cost = request.Cost;
        subscription.BillingFrequency = request.BillingFrequency;
        subscription.RenewalDate = request.RenewalDate;
        subscription.Category = request.Category.Trim();

        await dbContext.SaveChangesAsync(cancellationToken);
        return ToDto(subscription);
    }

    public async Task<bool> DeleteAsync(int userId, int id, CancellationToken cancellationToken)
    {
        var subscription = await dbContext.Subscriptions
            .SingleOrDefaultAsync(x => x.Id == id && x.UserId == userId, cancellationToken);

        if (subscription is null)
        {
            return false;
        }

        dbContext.Subscriptions.Remove(subscription);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    internal static SubscriptionDto ToDto(Subscription subscription) => new(
        subscription.Id,
        subscription.Name,
        subscription.Cost,
        subscription.BillingFrequency,
        subscription.RenewalDate,
        subscription.Category,
        subscription.CreatedAt);

    private static void Validate(SubscriptionRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.Cost < 0 || string.IsNullOrWhiteSpace(request.Category))
        {
            throw new InvalidOperationException("Name, category, and a non-negative cost are required.");
        }
    }
}
