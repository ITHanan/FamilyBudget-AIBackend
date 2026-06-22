using Domain.Enums;

namespace Domain.Entities;

public sealed class Subscription
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public required string Name { get; set; }
    public decimal Cost { get; set; }
    public BillingFrequency BillingFrequency { get; set; }
    public DateOnly RenewalDate { get; set; }
    public required string Category { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User? User { get; set; }
    public ICollection<Notification> Notifications { get; set; } = [];
}
