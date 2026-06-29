using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Domain.Entities;

namespace Infrastructure.Services;

public static class BankStatementTextParser
{
    private const string SwedbankColumnParser = "swedbank-column";
    private const string GenericRowParser = "generic-row";

    private static readonly Regex RowStartRegex = new(
        @"^\s*(?:\d+\s+)?(?:\d{4}[-/.]\d{2}[-/.]\d{2}|\d{2}[-/.]\d{2}[-/.]\d{4})\b",
        RegexOptions.Compiled);

    private static readonly Regex RowWithBalanceRegex = new(
        @"^\s*(?:\d+\s+)?(?<date>\d{4}[-/.]\d{2}[-/.]\d{2}|\d{2}[-/.]\d{2}[-/.]\d{4})\s+(?<description>.+?)\s+(?<amount>[+-]\d[\d\s]*(?:[.,]\d{2}))\s+(?<balance>[+-]?\d[\d\s]*(?:[.,]\d{2}))\s*(?<currency>EUR|SEK|kr)?\s*$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex RowWithUnsignedAmountAndBalanceRegex = new(
        @"^\s*(?:\d+\s+)?(?<date>\d{4}[-/.]\d{2}[-/.]\d{2}|\d{2}[-/.]\d{2}[-/.]\d{4})\s+(?<description>.+?)\s+(?<amount>\d[\d\s]*(?:[.,]\d{2}))\s+(?<balance>[+-]?\d[\d\s]*(?:[.,]\d{2}))\s*(?<currency>EUR|SEK|kr)?\s*$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex RowWithoutBalanceRegex = new(
        @"^\s*(?:\d+\s+)?(?<date>\d{4}[-/.]\d{2}[-/.]\d{2}|\d{2}[-/.]\d{2}[-/.]\d{4})\s+(?<description>.+?)\s+(?<amount>[+-]?\d[\d\s]*(?:[.,]\d{2}))\s*(?<currency>EUR|SEK|kr)?\s*$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex FlattenedRowRegex = new(
        @"(?:^|\s)(?:\d+\s+)?(?<date>\d{4}[-/.]\d{2}[-/.]\d{2}|\d{2}[-/.]\d{2}[-/.]\d{4})\s+(?<description>.*?)(?<amount>[+-]\d[\d\s]*(?:[.,]\d{2}))\s+(?<balance>[+-]?\d[\d\s]*(?:[.,]\d{2}))(?=\s+(?:\d+\s+)?(?:\d{4}[-/.]\d{2}[-/.]\d{2}|\d{2}[-/.]\d{2}[-/.]\d{4})\s+|$)",
        RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex FlattenedUnsignedRowRegex = new(
        @"(?:^|\s)(?:\d+\s+)?(?<date>\d{4}[-/.]\d{2}[-/.]\d{2}|\d{2}[-/.]\d{2}[-/.]\d{4})\s+(?<description>.*?)(?<amount>\d[\d\s]*(?:[.,]\d{2}))\s+(?<balance>[+-]?\d[\d\s]*(?:[.,]\d{2}))(?=\s+(?:\d+\s+)?(?:\d{4}[-/.]\d{2}[-/.]\d{2}|\d{2}[-/.]\d{2}[-/.]\d{4})\s+|$)",
        RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex IsoDateRegex = new(@"\b\d{4}-\d{2}-\d{2}\b", RegexOptions.Compiled);

    private static readonly Regex SwedishAmountRegex = new(
        @"(?<!\d)[+-]?(?:\d{1,3}(?: \d{3})+|\d+),\d{2}(?!\d)",
        RegexOptions.Compiled);

    private static readonly Regex SwedishRowWithAmountsRegex = new(
        @"(?<amount>[+-]?(?:\d{1,3}(?: \d{3})+|\d+),\d{2})\s+(?<balance>[+-]?(?:\d{1,3}(?: \d{3})+|\d+),\d{2})\s*$",
        RegexOptions.Compiled);

    public static IReadOnlyList<BankTransaction> ParseTransactions(
        string text,
        IReadOnlyList<TransactionCategoryRule> rules,
        string defaultCurrency = "EUR")
    {
        var visualRowTransactions = ParseSwedbankVisualRows(text, rules, defaultCurrency);
        if (visualRowTransactions.Count > 0)
        {
            MarkRecurringCandidates(visualRowTransactions);
            return visualRowTransactions;
        }

        var columnTransactions = ParseSwedbankColumnTransactions(text, rules, defaultCurrency);
        if (columnTransactions.Count > 0)
        {
            MarkRecurringCandidates(columnTransactions);
            return columnTransactions;
        }

        var rows = BuildCandidateRows(text);
        var transactions = new List<BankTransaction>();
        var seenRows = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        decimal? previousBalance = null;

        foreach (var row in rows)
        {
            if (!seenRows.Add(row))
            {
                continue;
            }

            if (TryParseRow(row, rules, defaultCurrency, previousBalance, out var transaction))
            {
                transactions.Add(transaction);
                previousBalance = transaction.Balance;
            }
        }

        MarkRecurringCandidates(transactions);
        return transactions;
    }

    public static string CreateDebugSample(string text)
    {
        var sample = NormalizeSpaces(text);
        var firstTokens = string.Join(", ", TokenizeLines(text).Take(30));
        var parserType = LooksLikeSwedbankColumnText(text) ? SwedbankColumnParser : GenericRowParser;
        var descriptionCount = ExtractSwedbankDescriptions(TokenizeLines(text)).Count;
        var dateCount = IsoDateRegex.Matches(text).Count;
        var amountCount = SwedishAmountRegex.Matches(text).Count;
        var prefix = $"Parser={parserType}; descriptions={descriptionCount}; dates={dateCount}; amounts={amountCount}; firstTokens=[{firstTokens}]. Sample=";
        var availableLength = Math.Max(0, 1000 - prefix.Length);
        var trimmedSample = sample.Length <= availableLength ? sample : sample[..availableLength];

        return $"{prefix}{trimmedSample}";
    }

    private static IReadOnlyList<BankTransaction> ParseSwedbankVisualRows(
        string text,
        IReadOnlyList<TransactionCategoryRule> rules,
        string defaultCurrency)
    {
        var lines = TokenizeLines(text);
        if (!LooksLikeSwedbankColumnText(lines))
        {
            return [];
        }

        var currency = lines.Any(line => line.Equals("SEK", StringComparison.OrdinalIgnoreCase) ||
            line.Contains("(SEK)", StringComparison.OrdinalIgnoreCase))
            ? "SEK"
            : defaultCurrency;
        var transactions = new List<BankTransaction>();

        foreach (var line in lines)
        {
            var amountMatch = SwedishRowWithAmountsRegex.Match(line);
            var dates = IsoDateRegex.Matches(line).Select(match => match.Value).ToList();
            if (!amountMatch.Success || dates.Count == 0)
            {
                continue;
            }

            var amount = ParseRequiredDecimal(amountMatch.Groups["amount"].Value);
            var balance = ParseRequiredDecimal(amountMatch.Groups["balance"].Value);
            if (!TryParseDate(dates[0], out var date))
            {
                continue;
            }

            var description = ExtractDescriptionFromVisualRow(line, dates, amountMatch.Index);
            if (string.IsNullOrWhiteSpace(description) || IsIgnorableLine(description))
            {
                continue;
            }

            var normalizedDescription = NormalizeDescription(description);
            var category = Categorize(normalizedDescription, amount, rules);

            transactions.Add(new BankTransaction
            {
                TransactionDate = date,
                Description = description,
                NormalizedDescription = normalizedDescription,
                Amount = amount,
                Balance = balance,
                Currency = currency,
                Category = category,
                RawText = line.Length <= 1000 ? line : line[..1000],
                IsIncome = amount > 0,
                IsInternalTransfer = IsInternalTransfer(normalizedDescription),
                NeedsReview = category == "Other" || description.Length < 4
            });
        }

        return transactions;
    }

    private static string ExtractDescriptionFromVisualRow(string line, IReadOnlyList<string> dates, int amountStartIndex)
    {
        var beforeAmount = line[..amountStartIndex].Trim();
        var lastDateIndex = dates
            .Select(date => beforeAmount.LastIndexOf(date, StringComparison.Ordinal))
            .Where(index => index >= 0)
            .DefaultIfEmpty(-1)
            .Max();

        var description = lastDateIndex >= 0
            ? beforeAmount[(lastDateIndex + "yyyy-MM-dd".Length)..]
            : beforeAmount;

        description = Regex.Replace(description, @"^\s*\d+\s+", string.Empty);
        description = Regex.Replace(description, @"\bPrivatkonto\s*\(SEK\)\b", string.Empty, RegexOptions.IgnoreCase);
        description = Regex.Replace(description, @"\bSEK\b", string.Empty, RegexOptions.IgnoreCase);
        return NormalizeSpaces(description);
    }

    private static IReadOnlyList<BankTransaction> ParseSwedbankColumnTransactions(
        string text,
        IReadOnlyList<TransactionCategoryRule> rules,
        string defaultCurrency)
    {
        var lines = TokenizeLines(text);
        if (!LooksLikeSwedbankColumnText(lines))
        {
            return [];
        }

        var descriptions = ExtractSwedbankDescriptions(lines);
        if (descriptions.Count == 0)
        {
            return [];
        }

        var firstDescriptionIndex = FindLineIndex(lines, descriptions[0]);
        if (firstDescriptionIndex < 0)
        {
            return [];
        }

        var dates = lines
            .Skip(firstDescriptionIndex + descriptions.Count)
            .SelectMany(line => IsoDateRegex.Matches(line).Select(match => match.Value))
            .ToList();

        var amountColumns = ExtractSwedishAmountColumns(lines, descriptions.Count);
        var amounts = amountColumns.Amounts;
        var balances = amountColumns.Balances;
        var transactionCount = new[] { descriptions.Count, dates.Count, amounts.Count }
            .Where(count => count > 0)
            .DefaultIfEmpty(0)
            .Min();

        if (transactionCount == 0)
        {
            return [];
        }

        var currency = lines.Any(line => line.Equals("SEK", StringComparison.OrdinalIgnoreCase) ||
            line.Contains("(SEK)", StringComparison.OrdinalIgnoreCase))
            ? "SEK"
            : defaultCurrency;
        var transactions = new List<BankTransaction>();
        for (var index = 0; index < transactionCount; index++)
        {
            if (!TryParseDate(dates[index], out var date))
            {
                continue;
            }

            var description = NormalizeSpaces(descriptions[index]);
            var amount = amounts[index];
            var normalizedDescription = NormalizeDescription(description);
            var category = Categorize(normalizedDescription, amount, rules);
            var balance = index < balances.Count ? balances[index] : (decimal?)null;
            var rawText = $"{date:yyyy-MM-dd} {description} {amount.ToString(CultureInfo.InvariantCulture)}";

            transactions.Add(new BankTransaction
            {
                TransactionDate = date,
                Description = description,
                NormalizedDescription = normalizedDescription,
                Amount = amount,
                Balance = balance,
                Currency = currency,
                Category = category,
                RawText = rawText.Length <= 1000 ? rawText : rawText[..1000],
                IsIncome = amount > 0,
                IsInternalTransfer = IsInternalTransfer(normalizedDescription),
                NeedsReview = category == "Other" || description.Length < 4
            });
        }

        return transactions;
    }

    private static List<string> TokenizeLines(string text)
    {
        return text
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(NormalizeSpaces)
            .Where(line => line.Length > 0)
            .ToList();
    }

    private static bool LooksLikeSwedbankColumnText(string text) => LooksLikeSwedbankColumnText(TokenizeLines(text));

    private static bool LooksLikeSwedbankColumnText(IReadOnlyList<string> lines)
    {
        return lines.Any(line => line.Contains("Beskrivning", StringComparison.OrdinalIgnoreCase)) &&
            (lines.Any(line => line.Contains("Belopp", StringComparison.OrdinalIgnoreCase)) ||
                lines.Any(line => line.Contains("Bokfört saldo", StringComparison.OrdinalIgnoreCase)) ||
                lines.Any(line => line.Contains("Bokfort saldo", StringComparison.OrdinalIgnoreCase)) ||
                lines.Any(line => line.Equals("Privatkonto (SEK)", StringComparison.OrdinalIgnoreCase)) ||
                lines.Any(line => line.Equals("Alla insättningar och uttag", StringComparison.OrdinalIgnoreCase)));
    }

    private static List<string> ExtractSwedbankDescriptions(IReadOnlyList<string> lines)
    {
        var start = FindLineIndex(lines, "Beskrivning");
        if (start < 0)
        {
            return [];
        }

        var descriptions = new List<string>();
        foreach (var line in lines.Skip(start + 1))
        {
            if (line.Equals("Ingående saldo", StringComparison.OrdinalIgnoreCase) ||
                line.Equals("Ingaende saldo", StringComparison.OrdinalIgnoreCase) ||
                line.Equals("Referens", StringComparison.OrdinalIgnoreCase) ||
                IsoDateRegex.IsMatch(line) ||
                IsSwedbankMetadataLine(line))
            {
                break;
            }

            if (!IsIgnorableLine(line) && !SwedishAmountRegex.IsMatch(line))
            {
                descriptions.Add(line);
            }
        }

        return descriptions;
    }

    private static (List<decimal> Amounts, List<decimal> Balances) ExtractSwedishAmountColumns(
        IReadOnlyList<string> lines,
        int expectedCount)
    {
        var amountHeaderIndex = FindLineIndex(lines, "Belopp");
        var balanceHeaderIndex = FindLineIndex(lines, "Bokfört saldo");
        if (balanceHeaderIndex < 0)
        {
            balanceHeaderIndex = FindLineIndex(lines, "Bokfort saldo");
        }

        var amounts = amountHeaderIndex >= 0
            ? ExtractAmountsAfterHeader(lines, amountHeaderIndex, balanceHeaderIndex, expectedCount)
            : new List<decimal>();

        var balances = balanceHeaderIndex >= 0
            ? ExtractAmountsAfterHeader(lines, balanceHeaderIndex, -1, expectedCount)
            : new List<decimal>();

        if (amounts.Count == 0)
        {
            var allAmounts = lines
                .SelectMany(line => SwedishAmountRegex.Matches(line).Select(match => match.Value))
                .Select(ParseRequiredDecimal)
                .ToList();

            amounts = allAmounts.Take(expectedCount).ToList();
            balances = allAmounts.Skip(expectedCount).Take(expectedCount).ToList();
        }

        return (amounts, balances);
    }

    private static List<decimal> ExtractAmountsAfterHeader(
        IReadOnlyList<string> lines,
        int startIndex,
        int stopIndex,
        int expectedCount)
    {
        var amounts = new List<decimal>();
        var endIndex = stopIndex > startIndex ? stopIndex : lines.Count;

        for (var index = startIndex + 1; index < endIndex && amounts.Count < expectedCount; index++)
        {
            if (IsSwedbankMetadataLine(lines[index]) && !SwedishAmountRegex.IsMatch(lines[index]))
            {
                continue;
            }

            foreach (Match match in SwedishAmountRegex.Matches(lines[index]))
            {
                amounts.Add(ParseRequiredDecimal(match.Value));
                if (amounts.Count == expectedCount)
                {
                    break;
                }
            }
        }

        return amounts;
    }

    private static int FindLineIndex(IReadOnlyList<string> lines, string value)
    {
        for (var index = 0; index < lines.Count; index++)
        {
            if (lines[index].Equals(value, StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return -1;
    }

    private static bool IsSwedbankMetadataLine(string value)
    {
        var normalized = NormalizeDescription(value);
        return normalized.Contains("SYSPR", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("EXPORTEN INKLUDERAR", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("REFERENS", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("BOKFORINGSDAG", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("TRANSAKTIONSDAG", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("VALUTADAG", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("BESKRIVNING", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("BELOPP", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("BOKFORT SALDO", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("SEK", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("PRIVATKONTO", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("ALLA INSATTNINGAR OCH UTTAG", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("INGAENDE SALDO", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("SALDO", StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> BuildCandidateRows(string text)
    {
        var lines = text
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(NormalizeSpaces)
            .Where(x => x.Length >= 8 && !IsIgnorableLine(x));

        string? current = null;
        foreach (var line in lines)
        {
            if (RowStartRegex.IsMatch(line))
            {
                if (!string.IsNullOrWhiteSpace(current))
                {
                    yield return current;
                }

                current = line;
            }
            else if (current is not null)
            {
                current = $"{current} {line}";
            }
        }

        if (!string.IsNullOrWhiteSpace(current))
        {
            yield return current;
        }

        var flattened = NormalizeSpaces(text);
        foreach (Match match in FlattenedRowRegex.Matches(flattened))
        {
            yield return NormalizeSpaces(match.Value);
        }

        foreach (Match match in FlattenedUnsignedRowRegex.Matches(flattened))
        {
            yield return NormalizeSpaces(match.Value);
        }
    }

    private static bool TryParseRow(
        string row,
        IReadOnlyList<TransactionCategoryRule> rules,
        string defaultCurrency,
        decimal? previousBalance,
        out BankTransaction transaction)
    {
        transaction = null!;

        var match = RowWithBalanceRegex.Match(row);
        var hasBalance = match.Success;
        var amountSignCanBeInferredFromBalance = false;
        if (!hasBalance)
        {
            match = RowWithUnsignedAmountAndBalanceRegex.Match(row);
            if (!match.Success)
            {
                match = RowWithoutBalanceRegex.Match(row);
                if (!match.Success)
                {
                    return false;
                }
            }
            else
            {
                hasBalance = true;
                amountSignCanBeInferredFromBalance = true;
            }
        }

        if (!TryParseDate(match.Groups["date"].Value, out var date) ||
            !TryParseDecimal(match.Groups["amount"].Value, out var amount))
        {
            return false;
        }

        decimal? balance = null;
        if (hasBalance && TryParseDecimal(match.Groups["balance"].Value, out var parsedBalance))
        {
            balance = parsedBalance;
        }

        if (amountSignCanBeInferredFromBalance)
        {
            amount = InferSignedAmount(amount, previousBalance, balance, row);
        }

        var description = NormalizeSpaces(match.Groups["description"].Value);
        if (string.IsNullOrWhiteSpace(description) || IsIgnorableLine(description))
        {
            return false;
        }

        var normalizedDescription = NormalizeDescription(description);
        var category = Categorize(normalizedDescription, amount, rules);
        var currency = match.Groups["currency"].Success && !string.IsNullOrWhiteSpace(match.Groups["currency"].Value)
            ? NormalizeCurrency(match.Groups["currency"].Value)
            : defaultCurrency;

        transaction = new BankTransaction
        {
            TransactionDate = date,
            Description = description,
            NormalizedDescription = normalizedDescription,
            Amount = amount,
            Balance = balance,
            Currency = currency,
            Category = category,
            RawText = row.Length <= 1000 ? row : row[..1000],
            IsIncome = amount > 0,
            IsInternalTransfer = IsInternalTransfer(normalizedDescription),
            NeedsReview = category == "Other" || description.Length < 4
        };

        return true;
    }

    private static bool TryParseDate(string value, out DateOnly date)
    {
        var formats = new[] { "yyyy-MM-dd", "yyyy/MM/dd", "yyyy.MM.dd", "dd-MM-yyyy", "dd/MM/yyyy", "dd.MM.yyyy" };
        return DateOnly.TryParseExact(value, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
    }

    private static bool TryParseDecimal(string value, out decimal amount)
    {
        var raw = value.Replace(" ", string.Empty);
        if (raw.Count(x => x == ',') == 1)
        {
            raw = raw.Replace(".", string.Empty).Replace(',', '.');
        }

        return decimal.TryParse(raw, NumberStyles.Number | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out amount);
    }

    private static decimal ParseRequiredDecimal(string value)
    {
        if (TryParseDecimal(value, out var amount))
        {
            return amount;
        }

        throw new FormatException($"Could not parse amount '{value}'.");
    }

    private static decimal InferSignedAmount(decimal amount, decimal? previousBalance, decimal? balance, string row)
    {
        if (amount < 0 || row.Contains('+'))
        {
            return amount;
        }

        if (previousBalance is not null && balance is not null)
        {
            var balanceChange = Math.Round(balance.Value - previousBalance.Value, 2);
            if (Math.Abs(balanceChange) == Math.Abs(amount))
            {
                return balanceChange;
            }
        }

        var normalized = NormalizeDescription(row);
        if (normalized.Contains("DEBIT", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("WITHDRAWAL", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("PAYMENT", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("PURCHASE", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("CARD", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("RENT", StringComparison.OrdinalIgnoreCase))
        {
            return -amount;
        }

        return amount;
    }

    private static string Categorize(string normalizedDescription, decimal amount, IReadOnlyList<TransactionCategoryRule> rules)
    {
        if (amount > 0)
        {
            return "Income";
        }

        var rule = rules.FirstOrDefault(x => normalizedDescription.Contains(x.MatchText, StringComparison.OrdinalIgnoreCase));
        return rule?.Category ?? "Other";
    }

    private static void MarkRecurringCandidates(IReadOnlyList<BankTransaction> transactions)
    {
        var recurringGroups = transactions
            .Where(x => x.Amount < 0 && !x.IsInternalTransfer)
            .GroupBy(x => new { x.NormalizedDescription, Amount = Math.Round(Math.Abs(x.Amount), 0) })
            .Where(x => x.Count() >= 2)
            .SelectMany(x => x);

        foreach (var transaction in recurringGroups)
        {
            transaction.IsRecurringCandidate = true;
        }
    }

    private static bool IsInternalTransfer(string normalizedDescription)
    {
        return normalizedDescription.Contains("SWISH", StringComparison.OrdinalIgnoreCase) ||
            normalizedDescription.Contains("TRANSFER", StringComparison.OrdinalIgnoreCase) ||
            normalizedDescription.Contains("OVERFORING", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsIgnorableLine(string value)
    {
        var normalized = NormalizeDescription(value);
        return normalized.Contains("KONTA PARSKATS", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("SWEDBANK", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("DATUMS", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("DATE", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("DESCRIPTION", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("SUMMA", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("AMOUNT", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("ATLIKUMS", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("BALANCE", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("TURNOVER", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("APGROZIJUMS", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("KOPA", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeSpaces(string value) => Regex.Replace(value.Trim(), @"\s+", " ");

    private static string NormalizeDescription(string value)
    {
        var withoutDiacritics = string.Concat(value.Normalize(NormalizationForm.FormD).Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark));
        return Regex.Replace(withoutDiacritics.ToUpperInvariant(), @"\s+", " ").Trim();
    }

    private static string NormalizeCurrency(string value)
    {
        return value.Equals("kr", StringComparison.OrdinalIgnoreCase) ? "SEK" : value.ToUpperInvariant();
    }
}
