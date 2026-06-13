using BotReport2026.Models;
using BotReport2026.Readers;

namespace BotReport2026.Rules;

public interface IRuleEngine
{
    string RuleCode { get; }
    (List<SbeRecord> sbeRecords, List<SaeRecord> saeRecords) Execute(RuleContext context);
}

public class RuleContext
{
    public List<RspTransaction> ReportingMonthRsp { get; set; } = new();
    public List<RspTransaction> HistoricalRsp { get; set; } = new();
    public List<TransactionReportRow> TransactionReport { get; set; } = new();
    public SanctionData SanctionData { get; set; } = new();
    public CrimeSuspiciousData CrimeSuspicious { get; set; } = new();
    public Dictionary<string, decimal> OccupationMap { get; set; } = new();
    public AppConfig Config { get; set; } = new();
    public DateTime ReportingMonth { get; set; }
    public Action<string> LogCallback { get; set; } = _ => { };
}
