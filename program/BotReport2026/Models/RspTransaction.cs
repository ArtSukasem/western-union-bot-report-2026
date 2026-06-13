namespace BotReport2026.Models;

public class RspTransaction
{
    public string MTCN { get; set; } = "";
    public string Direction { get; set; } = "";       // "IB" or "OB"
    public DateTime? TransactionDate { get; set; }
    public TimeSpan? TransactionTime { get; set; }
    public decimal Principal { get; set; }
    public string BranchAccountId { get; set; } = "";
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string PersonId { get; set; } = "";
    public string Occupation { get; set; } = "";
    public string SourceFile { get; set; } = "";

    public string FullName => $"{FirstName} {LastName}".Trim();
}
