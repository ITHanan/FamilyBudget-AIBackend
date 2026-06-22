namespace Domain.Entities;

public sealed class AIConversation
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public required string Title { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public User? User { get; set; }
    public ICollection<AIMessage> Messages { get; set; } = [];
}
