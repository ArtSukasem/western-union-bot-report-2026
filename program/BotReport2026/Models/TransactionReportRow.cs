namespace BotReport2026.Models;

public class TransactionReportRow
{
    public string MTCN { get; set; } = "";
    public string Status { get; set; } = "";
    public string SenderName { get; set; } = "";
    public string SenderIdNumber { get; set; } = "";
    public decimal? PrincipalAmount { get; set; }
    public string ErrorReason { get; set; } = "";
}
