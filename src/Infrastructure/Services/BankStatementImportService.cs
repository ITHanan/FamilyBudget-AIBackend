using Application.DTOs;
using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Text;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace Infrastructure.Services;

public sealed class BankStatementImportService(AppDbContext dbContext) : IBankStatementImportService
{
    private const long MaxFileSize = 10 * 1024 * 1024;

    public async Task<BankStatementImportResultDto> ImportAsync(
        int userId,
        string originalFileName,
        string contentType,
        long length,
        Stream pdfStream,
        CancellationToken cancellationToken)
    {
        ValidatePdf(originalFileName, contentType, length);

        var statement = new BankStatement
        {
            UserId = userId,
            OriginalFileName = Path.GetFileName(originalFileName),
            ImportStatus = "Processing"
        };

        dbContext.BankStatements.Add(statement);
        await dbContext.SaveChangesAsync(cancellationToken);

        try
        {
            var text = ExtractText(pdfStream);
            var rules = await LoadRulesAsync(userId, cancellationToken);
            var parsed = BankStatementTextParser.ParseTransactions(text, rules).ToList();

            if (parsed.Count == 0)
            {
                statement.ImportError = $"No transactions could be read from this PDF. Extracted text sample: {BankStatementTextParser.CreateDebugSample(text)}";
                throw new InvalidOperationException("No transactions could be read from this PDF. Try a statement with transaction rows that include date, description, amount, and balance.");
            }

            foreach (var transaction in parsed)
            {
                transaction.UserId = userId;
                transaction.BankStatementId = statement.Id;
            }

            statement.ImportStatus = "Imported";
            statement.ImportedAt = DateTime.UtcNow;
            statement.TransactionCount = parsed.Count;
            statement.StatementPeriodStart = parsed.Min(x => x.TransactionDate);
            statement.StatementPeriodEnd = parsed.Max(x => x.TransactionDate);

            dbContext.BankTransactions.AddRange(parsed);
            await dbContext.SaveChangesAsync(cancellationToken);

            return new BankStatementImportResultDto(
                statement.Id,
                parsed.Count,
                parsed.Count(x => x.NeedsReview),
                parsed.Count(x => x.IsRecurringCandidate));
        }
        catch (Exception ex)
        {
            statement.ImportStatus = "Failed";
            statement.ImportError ??= ex.Message;
            await dbContext.SaveChangesAsync(cancellationToken);
            throw;
        }
    }

    public async Task<IReadOnlyList<BankStatementDto>> GetStatementsAsync(int userId, CancellationToken cancellationToken)
    {
        return await dbContext.BankStatements
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.UploadedAt)
            .Select(x => new BankStatementDto(
                x.Id,
                x.OriginalFileName,
                x.BankName,
                x.StatementPeriodStart,
                x.StatementPeriodEnd,
                x.UploadedAt,
                x.ImportedAt,
                x.TransactionCount,
                x.ImportStatus,
                x.ImportError))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<BankTransactionDto>?> GetTransactionsAsync(int userId, int statementId, CancellationToken cancellationToken)
    {
        var statementBelongsToUser = await dbContext.BankStatements
            .AnyAsync(x => x.Id == statementId && x.UserId == userId, cancellationToken);

        if (!statementBelongsToUser)
        {
            return null;
        }

        return await dbContext.BankTransactions
            .Where(x => x.UserId == userId && x.BankStatementId == statementId)
            .OrderByDescending(x => x.TransactionDate)
            .ThenByDescending(x => x.Id)
            .Select(x => new BankTransactionDto(
                x.Id,
                x.BankStatementId,
                x.TransactionDate,
                x.Description,
                x.Amount,
                x.Balance,
                x.Currency,
                x.Category,
                x.RawText,
                x.IsIncome,
                x.IsInternalTransfer,
                x.IsRecurringCandidate,
                x.NeedsReview,
                x.CreatedAt))
            .ToListAsync(cancellationToken);
    }

    private static void ValidatePdf(string originalFileName, string contentType, long length)
    {
        if (length <= 0)
        {
            throw new InvalidOperationException("Upload a non-empty PDF file.");
        }

        if (length > MaxFileSize)
        {
            throw new InvalidOperationException("PDF file size cannot exceed 10 MB.");
        }

        if (!originalFileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) ||
            !contentType.Contains("pdf", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Only PDF bank statements are supported.");
        }
    }

    private static string ExtractText(Stream pdfStream)
    {
        using var document = PdfDocument.Open(pdfStream);
        return string.Join(Environment.NewLine, document.GetPages().Select(ExtractPageText));
    }

    private static string ExtractPageText(Page page)
    {
        var words = page.GetWords()
            .Where(word => !string.IsNullOrWhiteSpace(word.Text))
            .OrderByDescending(word => word.BoundingBox.Top)
            .ThenBy(word => word.BoundingBox.Left)
            .ToList();

        if (words.Count == 0)
        {
            return page.Text;
        }

        var lines = new List<List<Word>>();
        foreach (var word in words)
        {
            var line = lines.FirstOrDefault(candidate =>
                Math.Abs(candidate[0].BoundingBox.Top - word.BoundingBox.Top) <= 3);

            if (line is null)
            {
                lines.Add([word]);
            }
            else
            {
                line.Add(word);
            }
        }

        var builder = new StringBuilder();
        foreach (var line in lines)
        {
            var orderedWords = line.OrderBy(word => word.BoundingBox.Left).ToList();
            for (var index = 0; index < orderedWords.Count; index++)
            {
                if (index > 0)
                {
                    builder.Append(' ');
                }

                builder.Append(orderedWords[index].Text);
            }

            builder.AppendLine();
        }

        return builder.ToString();
    }

    private async Task<IReadOnlyList<TransactionCategoryRule>> LoadRulesAsync(int userId, CancellationToken cancellationToken)
    {
        var customRules = await dbContext.TransactionCategoryRules
            .Where(x => x.UserId == userId || x.UserId == null)
            .OrderByDescending(x => x.Priority)
            .ToListAsync(cancellationToken);

        return customRules
            .Concat(DefaultRules)
            .OrderByDescending(x => x.Priority)
            .ToList();
    }

    internal static readonly IReadOnlyList<TransactionCategoryRule> DefaultRules =
    [
        new() { MatchText = "ICA", Category = "Groceries", Priority = 100 },
        new() { MatchText = "WILLYS", Category = "Groceries", Priority = 100 },
        new() { MatchText = "COOP", Category = "Groceries", Priority = 100 },
        new() { MatchText = "LIDL", Category = "Groceries", Priority = 100 },
        new() { MatchText = "RIMI", Category = "Groceries", Priority = 100 },
        new() { MatchText = "MAXIMA", Category = "Groceries", Priority = 100 },
        new() { MatchText = "WILLYS GOTEB", Category = "Groceries", Priority = 120 },
        new() { MatchText = "ICA KVANTUM", Category = "Groceries", Priority = 120 },
        new() { MatchText = "STUDIEHJALP", Category = "Income", Priority = 120 },
        new() { MatchText = "BOSTADSBIDRA", Category = "Income", Priority = 120 },
        new() { MatchText = "JULA", Category = "Shopping", Priority = 100 },
        new() { MatchText = "SPOTIFY", Category = "Subscriptions", Priority = 100 },
        new() { MatchText = "NETFLIX", Category = "Subscriptions", Priority = 100 },
        new() { MatchText = "DISNEY", Category = "Subscriptions", Priority = 100 },
        new() { MatchText = "APPLE", Category = "Subscriptions", Priority = 90 },
        new() { MatchText = "MICROSOFT", Category = "Subscriptions", Priority = 90 },
        new() { MatchText = "SL", Category = "Transport", Priority = 80 },
        new() { MatchText = "UBER", Category = "Transport", Priority = 80 },
        new() { MatchText = "BOLT", Category = "Transport", Priority = 80 },
        new() { MatchText = "HYRA", Category = "Housing", Priority = 80 },
        new() { MatchText = "RENT", Category = "Housing", Priority = 80 },
        new() { MatchText = "UDENS", Category = "Utilities", Priority = 80 },
        new() { MatchText = "APOTEK", Category = "Healthcare", Priority = 80 },
        new() { MatchText = "RESTAUR", Category = "Restaurants", Priority = 80 },
        new() { MatchText = "CAFE", Category = "Restaurants", Priority = 80 },
        new() { MatchText = "AVANZA", Category = "Savings", Priority = 80 },
        new() { MatchText = "NORDNET", Category = "Savings", Priority = 80 }
    ];
}
