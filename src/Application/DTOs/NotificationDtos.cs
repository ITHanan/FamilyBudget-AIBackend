namespace Application.DTOs;

public sealed record NotificationDto(int Id, int SubscriptionId, string Message, bool IsRead, DateTime CreatedAt);
