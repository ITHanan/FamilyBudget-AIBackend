namespace Application.DTOs;

public sealed class FinancialHealthDto
{
    public int Score { get; set; }
    public string Status { get; set; } = string.Empty;
    public decimal PotentialMonthlySavings { get; set; }
    public string Recommendation { get; set; } = string.Empty;
}
