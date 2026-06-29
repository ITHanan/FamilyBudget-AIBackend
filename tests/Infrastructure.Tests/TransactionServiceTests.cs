using Application.DTOs;
using Domain.Entities;
using Infrastructure.Data;
using Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Infrastructure.Tests;

public sealed class TransactionServiceTests
{
    [Fact]
    public async Task UpdateCategoryAsync_RememberRuleUpdatesMatchingImportedTransactions()
    {
        await using var dbContext = CreateDbContext();
        var user = CreateUser();
        var statement = new BankStatement { User = user, OriginalFileName = "statement.pdf", ImportStatus = "Imported" };
        var selected = CreateTransaction(user, statement, "Swish Lisebe", "SWISH LISEBE", -150m);
        var matching = CreateTransaction(user, statement, "Swish Lisebe", "SWISH LISEBE", -200m);
        var other = CreateTransaction(user, statement, "ICA KVANTUM", "ICA KVANTUM", -109.03m);
        dbContext.AddRange(user, statement, selected, matching, other);
        await dbContext.SaveChangesAsync();

        var service = new TransactionService(dbContext);

        var result = await service.UpdateCategoryAsync(
            user.Id,
            selected.Id,
            new UpdateTransactionCategoryRequest("Entertainment", true),
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("Entertainment", result.Category);
        Assert.Equal("Entertainment", selected.Category);
        Assert.Equal("Entertainment", matching.Category);
        Assert.False(selected.NeedsReview);
        Assert.False(matching.NeedsReview);
        Assert.Equal("Other", other.Category);

        var rule = Assert.Single(await dbContext.TransactionCategoryRules.ToListAsync());
        Assert.Equal("SWISH LISEBE", rule.MatchText);
        Assert.Equal("Entertainment", rule.Category);
    }

    [Fact]
    public async Task UpdateCategoryAsync_RememberRuleRefreshesExistingRule()
    {
        await using var dbContext = CreateDbContext();
        var user = CreateUser();
        var statement = new BankStatement { User = user, OriginalFileName = "statement.pdf", ImportStatus = "Imported" };
        var transaction = CreateTransaction(user, statement, "JULA SVERIGE", "JULA SVERIGE", -39.30m);
        dbContext.AddRange(user, statement, transaction, new TransactionCategoryRule
        {
            User = user,
            MatchText = "JULA SVERIGE",
            Category = "Other",
            Priority = 50
        });
        await dbContext.SaveChangesAsync();

        var service = new TransactionService(dbContext);

        await service.UpdateCategoryAsync(
            user.Id,
            transaction.Id,
            new UpdateTransactionCategoryRequest("Shopping", true),
            CancellationToken.None);

        var rule = Assert.Single(await dbContext.TransactionCategoryRules.ToListAsync());
        Assert.Equal("Shopping", rule.Category);
        Assert.Equal(200, rule.Priority);
    }

    private static User CreateUser() => new()
    {
        Username = Guid.NewGuid().ToString("N"),
        FirstName = "Test",
        LastName = "User",
        Email = $"{Guid.NewGuid():N}@example.com",
        PasswordHash = "hash"
    };

    private static BankTransaction CreateTransaction(
        User user,
        BankStatement statement,
        string description,
        string normalizedDescription,
        decimal amount) => new()
        {
            User = user,
            BankStatement = statement,
            TransactionDate = new DateOnly(2026, 6, 23),
            Description = description,
            NormalizedDescription = normalizedDescription,
            Amount = amount,
            Currency = "SEK",
            Category = "Other",
            RawText = description,
            NeedsReview = true
        };

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }
}
