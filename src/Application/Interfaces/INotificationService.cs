using Application.DTOs;

namespace Application.Interfaces;

public interface INotificationService
{
    Task<IReadOnlyList<NotificationDto>> GetAllAsync(int userId, CancellationToken cancellationToken);
    Task<bool> MarkReadAsync(int userId, int id, CancellationToken cancellationToken);
}
