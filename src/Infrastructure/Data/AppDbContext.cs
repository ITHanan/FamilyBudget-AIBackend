using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();
    public DbSet<AIConversation> AIConversations => Set<AIConversation>();
    public DbSet<AIMessage> AIMessages => Set<AIMessage>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<BankStatement> BankStatements => Set<BankStatement>();
    public DbSet<BankTransaction> BankTransactions => Set<BankTransaction>();
    public DbSet<TransactionCategoryRule> TransactionCategoryRules => Set<TransactionCategoryRule>();
    public DbSet<UserSuggestion> UserSuggestions => Set<UserSuggestion>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(x => x.Email).IsUnique();
            entity.HasIndex(x => x.Username).IsUnique();
            entity.Property(x => x.Username).HasMaxLength(64).IsRequired();
            entity.Property(x => x.FirstName).HasMaxLength(100).IsRequired();
            entity.Property(x => x.LastName).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Email).HasMaxLength(256).IsRequired();
            entity.Property(x => x.PasswordHash).HasMaxLength(512).IsRequired();
        });

        modelBuilder.Entity<BankStatement>(entity =>
        {
            entity.Property(x => x.OriginalFileName).HasMaxLength(260).IsRequired();
            entity.Property(x => x.BankName).HasMaxLength(120);
            entity.Property(x => x.ImportStatus).HasMaxLength(32).IsRequired();
            entity.Property(x => x.ImportError).HasMaxLength(1000);
            entity.HasOne(x => x.User).WithMany(x => x.BankStatements).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<BankTransaction>(entity =>
        {
            entity.Property(x => x.Description).HasMaxLength(500).IsRequired();
            entity.Property(x => x.NormalizedDescription).HasMaxLength(500).IsRequired();
            entity.Property(x => x.Currency).HasMaxLength(3).IsRequired();
            entity.Property(x => x.Category).HasMaxLength(100).IsRequired();
            entity.Property(x => x.RawText).HasMaxLength(1000).IsRequired();
            entity.Property(x => x.Amount).HasColumnType("decimal(18,2)");
            entity.Property(x => x.Balance).HasColumnType("decimal(18,2)");
            entity.HasIndex(x => new { x.UserId, x.TransactionDate });
            entity.HasOne(x => x.User).WithMany(x => x.BankTransactions).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.BankStatement).WithMany(x => x.Transactions).HasForeignKey(x => x.BankStatementId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TransactionCategoryRule>(entity =>
        {
            entity.Property(x => x.MatchText).HasMaxLength(160).IsRequired();
            entity.Property(x => x.Category).HasMaxLength(100).IsRequired();
            entity.HasIndex(x => new { x.UserId, x.MatchText });
            entity.HasOne(x => x.User).WithMany(x => x.TransactionCategoryRules).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<UserSuggestion>(entity =>
        {
            entity.Property(x => x.Message).HasMaxLength(2000).IsRequired();
            entity.Property(x => x.RecipientEmail).HasMaxLength(256).IsRequired();
            entity.HasIndex(x => new { x.UserId, x.CreatedAt });
            entity.HasOne(x => x.User).WithMany(x => x.UserSuggestions).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Subscription>(entity =>
        {
            entity.Property(x => x.Name).HasMaxLength(160).IsRequired();
            entity.Property(x => x.Category).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Cost).HasColumnType("decimal(18,2)");
            entity.Property(x => x.BillingFrequency).HasConversion<string>().HasMaxLength(32);
            entity.HasOne(x => x.User).WithMany(x => x.Subscriptions).HasForeignKey(x => x.UserId);
        });

        modelBuilder.Entity<AIConversation>(entity =>
        {
            entity.Property(x => x.Title).HasMaxLength(160).IsRequired();
            entity.HasOne(x => x.User).WithMany(x => x.AIConversations).HasForeignKey(x => x.UserId);
        });

        modelBuilder.Entity<AIMessage>(entity =>
        {
            entity.Property(x => x.Role).HasMaxLength(32).IsRequired();
            entity.Property(x => x.Content).HasMaxLength(8000).IsRequired();
            entity.HasOne(x => x.Conversation).WithMany(x => x.Messages).HasForeignKey(x => x.ConversationId);
        });

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.Property(x => x.Message).HasMaxLength(500).IsRequired();
            entity.HasOne(x => x.User).WithMany(x => x.Notifications).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Subscription).WithMany(x => x.Notifications).HasForeignKey(x => x.SubscriptionId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
