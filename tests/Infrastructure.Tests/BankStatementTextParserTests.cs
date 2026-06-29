using Domain.Entities;
using Infrastructure.Services;
using Xunit;

namespace Infrastructure.Tests;

public sealed class BankStatementTextParserTests
{
    private static readonly IReadOnlyList<TransactionCategoryRule> Rules =
    [
        new() { MatchText = "UDENS", Category = "Utilities", Priority = 100 },
        new() { MatchText = "RENT", Category = "Housing", Priority = 100 }
    ];

    [Fact]
    public void ParseTransactions_ParsesLatvianSwedbankRows()
    {
        const string text = """
            Konta parskats
            Swedbank
            Datums Sanemejs / Maksatajs Informacija sanemejam Summa Atlikums
            1 01.11.2023 Kartes izgatavosanas un piegades maksa -2.00 -1.64
            2 01.11.2023 Kartes menesa maksa -0.85 -2.49
            3 01.11.2023 SABINE RUMPE G TV G TV g +15.00 12.51
            4 02.11.2023 JEKABPILS UDENS SIA Maksajums -13.00 169.51
            """;

        var transactions = BankStatementTextParser.ParseTransactions(text, Rules);

        Assert.Equal(4, transactions.Count);
        Assert.Equal(new DateOnly(2023, 11, 1), transactions[0].TransactionDate);
        Assert.Equal(-2.00m, transactions[0].Amount);
        Assert.Equal(-1.64m, transactions[0].Balance);
        Assert.Equal("EUR", transactions[0].Currency);
        Assert.Contains("Kartes", transactions[0].Description);
    }

    [Fact]
    public void ParseTransactions_ParsesPositiveIncomeRows()
    {
        const string text = "3 01.11.2023 SABINE RUMPE G TV G TV g +15.00 12.51";

        var transaction = Assert.Single(BankStatementTextParser.ParseTransactions(text, Rules));

        Assert.Equal(15.00m, transaction.Amount);
        Assert.True(transaction.IsIncome);
        Assert.Equal("Income", transaction.Category);
    }

    [Fact]
    public void ParseTransactions_ParsesNegativeExpenseRows()
    {
        const string text = "4 02.11.2023 JEKABPILS UDENS SIA Maksajums -13.00 169.51";

        var transaction = Assert.Single(BankStatementTextParser.ParseTransactions(text, Rules));

        Assert.Equal(-13.00m, transaction.Amount);
        Assert.False(transaction.IsIncome);
        Assert.Equal("Utilities", transaction.Category);
    }

    [Fact]
    public void ParseTransactions_HandlesLatvianCharacters()
    {
        const string text = "4 02.11.2023 J\u0112KABPILS \u016ADENS SIA Maks\u0101jums -13.00 169.51";

        var transaction = Assert.Single(BankStatementTextParser.ParseTransactions(text, Rules));

        Assert.Contains("Maks\u0101jums", transaction.Description);
        Assert.Equal("Utilities", transaction.Category);
    }

    [Fact]
    public void ParseTransactions_IgnoresHeaderFooterAndSummaryRows()
    {
        const string text = """
            Konta parskats
            Datums Sanemejs Summa Atlikums
            Debet apgrozijums -999.00
            1 01.11.2023 Kartes menesa maksa -0.85 -2.49
            Kredit apgrozijums +999.00
            """;

        var transactions = BankStatementTextParser.ParseTransactions(text, Rules);

        Assert.Single(transactions);
        Assert.Equal(-0.85m, transactions[0].Amount);
    }

    [Fact]
    public void ParseTransactions_ParsesSimpleIsoDateRows()
    {
        const string text = """
            2026-06-02 Salary +3250.00 8250.00
            2026-06-03 Rent Payment -1450.00 6800.00
            """;

        var transactions = BankStatementTextParser.ParseTransactions(text, Rules);

        Assert.Equal(2, transactions.Count);
        Assert.Equal(new DateOnly(2026, 6, 2), transactions[0].TransactionDate);
        Assert.Equal(3250.00m, transactions[0].Amount);
        Assert.Equal(new DateOnly(2026, 6, 3), transactions[1].TransactionDate);
        Assert.Equal(-1450.00m, transactions[1].Amount);
        Assert.Equal("Housing", transactions[1].Category);
    }

    [Fact]
    public void ParseTransactions_ParsesSwedbankColumnBasedPdfText()
    {
        const string text = """
            SysPr / Bl 2658 utg 1
            Exporten inkluderar
            Beskrivning
            Studiehjälp
            WILLYS GOTEB
            Bostadsbidra
            JULA SVERIGE
            ICA KVANTUM
            Swish Lisebe
            Swish Lisebe
            Glas ogin
            ICA KVANTUM
            Ingående saldo
            2026-06-26
            2026-06-25
            2026-06-24
            2026-06-23
            2026-06-23
            2026-06-23
            2026-06-23
            2026-06-22
            2026-06-22
            Referens
            Privatkonto (SEK)
            SEK
            Alla insättningar och uttag
            2026-06-22 till 2026-06-28
            2026-07-01
            2026-06-25
            2026-06-29
            2026-06-23
            2026-06-23
            2026-06-23
            2026-06-23
            2026-06-22
            2026-06-21
            Transaktionsdag
            2026-06-26
            2026-06-25
            2026-06-24
            2026-06-23
            2026-06-23
            2026-06-23
            2026-06-23
            2026-06-22
            2026-06-22
            Belopp
            625,00
            -198,70
            3 600,00
            -39,30
            -109,03
            -150,00
            -200,00
            -59,51
            -351,00
            Bokfört saldo
            5 358,46
            4 733,46
            4 932,16
            1 332,16
            1 371,46
            1 480,49
            1 630,49
            1 830,49
            1 890,00
            """;

        var transactions = BankStatementTextParser.ParseTransactions(text, Rules);

        Assert.Equal(9, transactions.Count);
        Assert.Equal(new DateOnly(2026, 6, 26), transactions[0].TransactionDate);
        Assert.Equal("Studiehjälp", transactions[0].Description);
        Assert.Equal(625.00m, transactions[0].Amount);
        Assert.Equal(5358.46m, transactions[0].Balance);
        Assert.Equal("SEK", transactions[0].Currency);
        Assert.Equal("WILLYS GOTEB", transactions[1].Description);
        Assert.Equal(-198.70m, transactions[1].Amount);
        Assert.Equal("Bostadsbidra", transactions[2].Description);
        Assert.Equal(3600.00m, transactions[2].Amount);
        Assert.Equal("JULA SVERIGE", transactions[3].Description);
        Assert.Equal(-39.30m, transactions[3].Amount);
        Assert.Equal("ICA KVANTUM", transactions[4].Description);
        Assert.Equal(-109.03m, transactions[4].Amount);
    }

    [Fact]
    public void ParseTransactions_ParsesSwedbankVisualTableRows()
    {
        const string text = """
            Transaktioner Hanan Mohamed Ahmed
            Referens Bokföringsdag Transaktionsdag Valutadag Beskrivning Belopp Bokfört saldo
            Privatkonto (SEK)
            SEK
            Alla insättningar och uttag
            1 Privatkonto (SEK) 2026-06-26 2026-06-25 2026-06-29 Studiehjälp 625,00 5 358,46
            2 Privatkonto (SEK) 2026-06-25 2026-06-25 2026-06-25 WILLYS GOTEB -198,70 4 733,46
            3 Privatkonto (SEK) 2026-06-24 2026-06-23 2026-06-24 Bostadsbidra 3 600,00 4 932,16
            4 Privatkonto (SEK) 2026-06-23 2026-06-23 2026-06-23 JULA SVERIGE -39,30 1 332,16
            5 Privatkonto (SEK) 2026-06-23 2026-06-23 2026-06-23 ICA KVANTUM -109,03 1 371,46
            """;

        var transactions = BankStatementTextParser.ParseTransactions(text, Rules);

        Assert.Equal(5, transactions.Count);
        Assert.Equal("Studiehjälp", transactions[0].Description);
        Assert.Equal(625.00m, transactions[0].Amount);
        Assert.Equal(5358.46m, transactions[0].Balance);
        Assert.Equal("WILLYS GOTEB", transactions[1].Description);
        Assert.Equal(-198.70m, transactions[1].Amount);
        Assert.Equal("Bostadsbidra", transactions[2].Description);
        Assert.Equal(3600.00m, transactions[2].Amount);
        Assert.Equal("ICA KVANTUM", transactions[4].Description);
        Assert.Equal(-109.03m, transactions[4].Amount);
    }

    [Fact]
    public void CreateDebugSample_IncludesColumnParserDiagnostics()
    {
        const string text = """
            Beskrivning
            Studiehjälp
            WILLYS GOTEB
            Ingående saldo
            2026-06-26
            2026-06-25
            Privatkonto (SEK)
            Belopp
            625,00
            -198,70
            """;

        var debugSample = BankStatementTextParser.CreateDebugSample(text);

        Assert.Contains("Parser=swedbank-column", debugSample);
        Assert.Contains("descriptions=2", debugSample);
        Assert.Contains("dates=2", debugSample);
        Assert.Contains("amounts=2", debugSample);
        Assert.Contains("firstTokens=[Beskrivning, Studiehjälp", debugSample);
    }

    [Fact]
    public void ParseTransactions_InfersSignFromUnsignedAmountAndBalanceRows()
    {
        const string text = """
            Date Description Amount Balance
            2026-06-02 Salary 3250.00 8250.00
            2026-06-03 Rent Payment 1450.00 6800.00
            2026-06-04 Grocery Store 250.00 6550.00
            """;

        var transactions = BankStatementTextParser.ParseTransactions(text, Rules);

        Assert.Equal(3, transactions.Count);
        Assert.Equal(3250.00m, transactions[0].Amount);
        Assert.Equal(-1450.00m, transactions[1].Amount);
        Assert.Equal(-250.00m, transactions[2].Amount);
        Assert.Equal("Housing", transactions[1].Category);
    }

    [Fact]
    public void ParseTransactions_DoesNotDropDebitOrCreditDescriptions()
    {
        const string text = """
            2026-06-01 Credit Card Payment 100.00 900.00
            2026-06-02 Debit Card Purchase 25.00 875.00
            """;

        var transactions = BankStatementTextParser.ParseTransactions(text, Rules);

        Assert.Equal(2, transactions.Count);
        Assert.Equal(-100.00m, transactions[0].Amount);
        Assert.Equal(-25.00m, transactions[1].Amount);
    }

    [Fact]
    public void ParseTransactions_ParsesSimpleEuropeanDateRows()
    {
        const string text = """
            01.06.2026 Salary +3250.00 8250.00
            03.06.2026 Rent Payment -1450.00 6800.00
            """;

        var transactions = BankStatementTextParser.ParseTransactions(text, Rules);

        Assert.Equal(2, transactions.Count);
        Assert.Equal(new DateOnly(2026, 6, 1), transactions[0].TransactionDate);
        Assert.Equal(new DateOnly(2026, 6, 3), transactions[1].TransactionDate);
    }

    [Fact]
    public void ParseTransactions_ReturnsEmptyForUnsupportedText()
    {
        const string text = "This PDF has no recognizable transaction table rows.";

        var transactions = BankStatementTextParser.ParseTransactions(text, Rules);

        Assert.Empty(transactions);
        Assert.Contains("recognizable transaction", BankStatementTextParser.CreateDebugSample(text));
    }

    [Fact]
    public void ParseTransactions_HandlesRowsSplitAcrossLines()
    {
        const string text = """
            1 01.11.2023 Kartes izgatavosanas
            un piegades maksa -2.00 -1.64
            2 01.11.2023 Kartes menesa
            maksa -0.85 -2.49
            """;

        var transactions = BankStatementTextParser.ParseTransactions(text, Rules);

        Assert.Equal(2, transactions.Count);
        Assert.Contains("piegades maksa", transactions[0].Description);
        Assert.Equal(-0.85m, transactions[1].Amount);
    }
}
