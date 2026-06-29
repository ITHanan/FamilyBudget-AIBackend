namespace Domain.Entities;

public sealed class UserSuggestion
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public required string Message { get; set; }
    public required string RecipientEmail { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User? User { get; set; }
}
