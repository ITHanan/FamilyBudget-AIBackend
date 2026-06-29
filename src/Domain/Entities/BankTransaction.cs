namespace Domain.Entities;

public sealed class BankTransaction
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int BankStatementId { get; set; }
    public DateOnly TransactionDate { get; set; }
    public required string Description { get; set; }
    public required string NormalizedDescription { get; set; }
    public decimal Amount { get; set; }
    public decimal? Balance { get; set; }
    public required string Currency { get; set; }
    public required string Category { get; set; }
    public required string RawText { get; set; }
    public bool IsIncome { get; set; }
    public bool IsInternalTransfer { get; set; }
    public bool IsRecurringCandidate { get; set; }
    public bool NeedsReview { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User? User { get; set; }
    public BankStatement? BankStatement { get; set; }
}
