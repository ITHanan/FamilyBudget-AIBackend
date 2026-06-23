namespace Domain.Entities;

public sealed class User
{
    public int Id { get; set; }
    public required string Username { get; set; }
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public required string Email { get; set; }
    public required string PasswordHash { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Subscription> Subscriptions { get; set; } = [];
    public ICollection<AIConversation> AIConversations { get; set; } = [];
    public ICollection<Notification> Notifications { get; set; } = [];
}
