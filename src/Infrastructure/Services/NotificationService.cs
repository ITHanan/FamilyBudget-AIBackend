using Application.DTOs;
using Application.Interfaces;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services;

public sealed class NotificationService(AppDbContext dbContext) : INotificationService
{
    public async Task<IReadOnlyList<NotificationDto>> GetAllAsync(int userId, CancellationToken cancellationToken)
    {
        return await dbContext.Notifications
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new NotificationDto(x.Id, x.SubscriptionId, x.Message, x.IsRead, x.CreatedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> MarkReadAsync(int userId, int id, CancellationToken cancellationToken)
    {
        var notification = await dbContext.Notifications
            .SingleOrDefaultAsync(x => x.Id == id && x.UserId == userId, cancellationToken);

        if (notification is null)
        {
            return false;
        }

        notification.IsRead = true;
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}
