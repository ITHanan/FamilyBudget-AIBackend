namespace Domain.Entities;

public sealed class BankStatement
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public required string OriginalFileName { get; set; }
    public string? BankName { get; set; }
    public DateOnly? StatementPeriodStart { get; set; }
    public DateOnly? StatementPeriodEnd { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ImportedAt { get; set; }
    public int TransactionCount { get; set; }
    public required string ImportStatus { get; set; }
    public string? ImportError { get; set; }

    public User? User { get; set; }
    public ICollection<BankTransaction> Transactions { get; set; } = [];
}
