namespace BotReport2026.Models;

public class AppConfig
{
    // RSP input — cl2 Transaction_Status values kept. Empty list = no filtering.
    // Sheet (A) พฤติกรรมความเสี่ยง: "ต้องดู status cl2 ก่อนว่าเป็น PAID, UNPAID เท่านั้นที่เอามาทำรายงาน".
    public List<string> RspIncludedStatuses { get; set; } = new() { "PAID", "UNPAID" };

    // DS_SAE fields 11 & 15 — branches treated as the online channel (330004) and
    // therefore having no physical transaction location.
    public List<string> SaeOnlineChannelBranches { get; set; } = new() { "ATH170025", "ATH170014" };

    // Rule 202 — outbound only, and only these branches (the online channel). Empty = every branch.
    public int Rule202PriorMonths { get; set; } = 6;
    public decimal Rule202Multiplier { get; set; } = 3m;
    public List<string> Rule202Branches { get; set; } = new() { "ATH170025", "ATH170014" };

    // Occupation lookup (Rule 203 retail income + DS_SBE field 16 BoT code): minimum
    // similarity, in percent, for matching the free-text cl114 / cl104 when the
    // cl89 / cl54 dropdown value is not in the lookup.
    public int OccupationFuzzyThresholdPercent { get; set; } = 80;

    // Rule 203 retail — outbound only; the online-channel branches are excluded
    public int Rule203RollingMonths { get; set; } = 3;
    public decimal Rule203Multiplier { get; set; } = 3m;
    public List<string> Rule203ExcludedBranches { get; set; } = new() { "ATH170025", "ATH170014" };
    // Expected monthly income for a person whose occupation matches no lookup row with an
    // income (the "อื่น ๆ" case) — used instead of skipping them.
    public decimal Rule203OtherMonthlyIncome { get; set; } = 49999m;

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
