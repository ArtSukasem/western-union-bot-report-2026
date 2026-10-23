namespace BotReport2026.Models;

public class AppConfig
{
    // RSP input — cl2 Transaction_Status values kept. Empty list = no filtering.
    // Sheet (A) พฤติกรรมความเสี่ยง: "ต้องดู status cl2 ก่อนว่าเป็น PAID, UNPAID เท่านั้นที่เอามาทำรายงาน".
    public List<string> RspIncludedStatuses { get; set; } = new() { "PAID", "UNPAID" };

    // DS_SAE fields 11 & 15 — branches treated as the online channel (330004) and
    // therefore having no physical transaction location.
    public List<string> SaeOnlineChannelBranches { get; set; } = new() { "ATH170025", "ATH170014" };

    // Rule 202
    public int Rule202PriorMonths { get; set; } = 6;
    public decimal Rule202Multiplier { get; set; } = 3m;

    // Rule 203 retail
    public int Rule203RollingMonths { get; set; } = 3;
    public decimal Rule203Multiplier { get; set; } = 3m;
    public List<string> Rule203ExcludedBranches { get; set; } = new() { "ATH170025" };

    // Rule 206
    public List<string> Rule206HighRiskBranches { get; set; } = new();
    public decimal Rule206PrincipalThreshold { get; set; } = 700000m;
    public int Rule206TxnCountThreshold { get; set; } = 20;

    // Rule 208
    public decimal Rule208SubThreshold { get; set; } = 50000m;
    public int Rule208DailyCount { get; set; } = 10;
    public decimal Rule208DailyAmount { get; set; } = 150000m;
    public int Rule208WeeklyCount { get; set; } = 20;
    public decimal Rule208WeeklyAmount { get; set; } = 150000m;
    public int Rule208MonthlyCount { get; set; } = 50;
    public decimal Rule208MonthlyAmount { get; set; } = 150000m;

    // Rule 209
    public int Rule209ConsecutiveHours { get; set; } = 16;
}
