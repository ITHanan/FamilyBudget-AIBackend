namespace Domain.Entities;

public sealed class AIMessage
{
    public int Id { get; set; }
    public int ConversationId { get; set; }
    public required string Role { get; set; }
    public required string Content { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public AIConversation? Conversation { get; set; }
}
