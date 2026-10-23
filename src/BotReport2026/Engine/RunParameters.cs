using BotReport2026.Models;

namespace BotReport2026.Engine;

public class RunParameters
{
    public List<string> ReportingMonthInboundFiles { get; set; } = new();
    public List<string> ReportingMonthOutboundFiles { get; set; } = new();
    public List<string> HistoricalInboundFiles { get; set; } = new();
    public List<string> HistoricalOutboundFiles { get; set; } = new();
    public List<string> TransactionReportFiles { get; set; } = new();
    public string SanctionListPath { get; set; } = "";
    public string CrimeListPath { get; set; } = "";
    public string OccupationMapPath { get; set; } = "";
    /// <summary>Folder holding the DS_SBE/DS_SAE reference tables (SAE-lookups/).</summary>
    public string ReferenceDataDirectory { get; set; } = "";
    public DateTime ReportingMonth { get; set; }
    public AppConfig Config { get; set; } = new();
    public string OutputDirectory { get; set; } = "";
}
