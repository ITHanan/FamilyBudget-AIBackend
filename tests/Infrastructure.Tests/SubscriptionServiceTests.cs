using Application.DTOs;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Data;
using Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Infrastructure.Tests;

public sealed class SubscriptionServiceTests
{
    [Fact]
    public async Task CreateAsync_TrimsInputAndPersistsSubscription()
    {
        await using var dbContext = CreateDbContext();
        var user = new User
        {
            Username = "testuser",
            FirstName = "Test",
            LastName = "User",
            Email = "test@example.com",
            PasswordHash = "hash"
        };
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var service = new SubscriptionService(dbContext);
        var request = new SubscriptionRequest(
            "  Netflix  ",
            149m,
            BillingFrequency.Monthly,
            new DateOnly(2026, 7, 1),
            "  Entertainment  ");

        var result = await service.CreateAsync(user.Id, request, CancellationToken.None);

        Assert.Equal("Netflix", result.Name);
        Assert.Equal("Entertainment", result.Category);
        Assert.Equal(149m, result.Cost);
        Assert.Single(await dbContext.Subscriptions.ToListAsync());
    }

    [Fact]
    public async Task CreateAsync_RejectsInvalidSubscription()
    {
        await using var dbContext = CreateDbContext();
        var service = new SubscriptionService(dbContext);
        var request = new SubscriptionRequest(
            "",
            -1m,
            BillingFrequency.Monthly,
            new DateOnly(2026, 7, 1),
            "");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateAsync(1, request, CancellationToken.None));

        Assert.Equal("Name, category, and a non-negative cost are required.", exception.Message);
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }
}
