using Application.DTOs;

namespace Application.Interfaces;

public interface ISubscriptionService
{
    Task<IReadOnlyList<SubscriptionDto>> GetAllAsync(int userId, CancellationToken cancellationToken);
    Task<SubscriptionDto?> GetByIdAsync(int userId, int id, CancellationToken cancellationToken);
    Task<SubscriptionDto> CreateAsync(int userId, SubscriptionRequest request, CancellationToken cancellationToken);
    Task<SubscriptionDto?> UpdateAsync(int userId, int id, SubscriptionRequest request, CancellationToken cancellationToken);
    Task<bool> DeleteAsync(int userId, int id, CancellationToken cancellationToken);
}
