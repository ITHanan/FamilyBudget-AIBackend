using Application.DTOs;
using Domain.Entities;

namespace Application.Interfaces;

public interface IFinancialHealthService
{
    FinancialHealthDto Calculate(IReadOnlyList<Subscription> subscriptions, DateOnly today);
}
