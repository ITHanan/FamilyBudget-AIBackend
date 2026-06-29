using System.ComponentModel.DataAnnotations;

namespace Application.DTOs;

public sealed record BankStatementDto(
    int Id,
    string OriginalFileName,
    string? BankName,
    DateOnly? StatementPeriodStart,
    DateOnly? StatementPeriodEnd,
    DateTime UploadedAt,
    DateTime? ImportedAt,
    int TransactionCount,
    string ImportStatus,
    string? ImportError);

public sealed record BankTransactionDto(
    int Id,
    int BankStatementId,
    DateOnly TransactionDate,
    string Description,
    decimal Amount,
    decimal? Balance,
    string Currency,
    string Category,
    string RawText,
    bool IsIncome,
    bool IsInternalTransfer,
    bool IsRecurringCandidate,
    bool NeedsReview,
    DateTime CreatedAt);

public sealed record BankStatementImportResultDto(
    int StatementId,
    int TransactionCount,
    int NeedsReviewCount,
    int RecurringCandidateCount);

public sealed record UpdateTransactionCategoryRequest(
    [Required, MaxLength(100)] string Category,
    bool RememberRule);

public sealed record CategoryTotalDto(string Category, decimal Amount, int Count);

public sealed record TransactionSummaryDto(
    decimal Income,
    decimal Expenses,
    decimal NetSavings,
    decimal SavingsRate,
    IReadOnlyList<CategoryTotalDto> CategoryTotals,
    IReadOnlyList<BankTransactionDto> LargestTransactions,
    decimal RecurringPaymentTotal);

public sealed record RecurringPaymentCandidateDto(
    string MerchantName,
    decimal AverageAmount,
    string BillingFrequency,
    int Confidence,
    DateOnly FirstSeenDate,
    DateOnly LastSeenDate,
    string SuggestedCategory,
    int TransactionCount);
