using Application.DTOs;

namespace Application.Interfaces;

public interface ITransactionService
{
    Task<BankTransactionDto?> UpdateCategoryAsync(int userId, int transactionId, UpdateTransactionCategoryRequest request, CancellationToken cancellationToken);
    Task<TransactionSummaryDto> GetSummaryAsync(int userId, DateOnly? from, DateOnly? to, CancellationToken cancellationToken);
    Task<IReadOnlyList<RecurringPaymentCandidateDto>> GetRecurringCandidatesAsync(int userId, CancellationToken cancellationToken);
}
