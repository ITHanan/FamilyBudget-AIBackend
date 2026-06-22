using Application.DTOs;

namespace Application.Interfaces;

public interface IDashboardService
{
    Task<DashboardSummaryDto> GetSummaryAsync(int userId, CancellationToken cancellationToken);
}
