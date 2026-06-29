namespace Domain.Entities;

public sealed class TransactionCategoryRule
{
    public int Id { get; set; }
    public int? UserId { get; set; }
    public required string MatchText { get; set; }
    public required string Category { get; set; }
    public int Priority { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User? User { get; set; }
}
