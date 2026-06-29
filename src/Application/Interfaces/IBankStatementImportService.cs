using Application.DTOs;

namespace Application.Interfaces;

public interface IBankStatementImportService
{
    Task<BankStatementImportResultDto> ImportAsync(
        int userId,
        string originalFileName,
        string contentType,
        long length,
        Stream pdfStream,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<BankStatementDto>> GetStatementsAsync(int userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<BankTransactionDto>> GetTransactionsAsync(int userId, int statementId, CancellationToken cancellationToken);
}
