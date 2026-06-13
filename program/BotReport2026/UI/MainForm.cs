using BotReport2026.Config;
using BotReport2026.Engine;
using BotReport2026.Models;

namespace BotReport2026.UI;

public partial class MainForm : Form
{
    private AppConfig _config = ConfigManager.Load();
    private CancellationTokenSource? _cts;

    // Default paths relative to repo root
    private static readonly string RepoRoot =
        Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\..\.."));

    public static string SanctionListPath =>
        Path.Combine(RepoRoot, "lookups", "saction_list.xlsx");
    public static string CrimeListPath =>
        Path.Combine(RepoRoot, "lookups", "financial_crime_individuals_suspicious_counter_customer_behavior.xlsx");
    public static string OccupationMapPath =>
        Path.Combine(RepoRoot, "mapper", "list_of_occupation_expected_monthly_income.xlsx");
    public static string OutputDirectory =>
        Path.Combine(RepoRoot, "report-results");
    public static string InputRspDir =>
        Path.Combine(RepoRoot, "input-rsp");
    public static string InputTxnReportDir =>
        Path.Combine(RepoRoot, "input-transaction-report");

    /// <summary>Creates all input/lookup/output folders so users have a place to drop files.</summary>
    public static void EnsureFolders()
    {
        foreach (var dir in new[]
        {
            InputRspDir, InputTxnReportDir,
            Path.Combine(RepoRoot, "lookups"),
            Path.Combine(RepoRoot, "mapper"),
            OutputDirectory
        })
        {
            try { Directory.CreateDirectory(dir); } catch { }
        }
    }

    public MainForm()
    {
        InitializeComponent();
        var ver = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
        Text = $"BotReport 2026 — SBR Report Generator  v{ver!.Major}.{ver.Minor}";
        MinimumSize = new Size(900, 800);
        Size = new Size(1000, 900);
        StartPosition = FormStartPosition.CenterScreen;

        // Apply settings to TabSettings
        _tabSettings.SetConfig(_config);

        // Auto-load input files from folders on startup
        EnsureFolders();
        var latest = _tabFileSelection.LoadFromFolders();
        if (latest.HasValue)
        {
            _tabRunOutput.SetReportingMonth(latest.Value);
            _tabFileSelection.ApplyReportingMonth(latest.Value);
        }
        else
        {
            _tabFileSelection.ApplyReportingMonth(_tabRunOutput.GetReportingMonth());
        }
    }

    /// <summary>Called when the reporting-month picker changes — re-split files by month.</summary>
    internal void OnReportingMonthChanged(DateTime month)
    {
        _tabFileSelection.ApplyReportingMonth(month);
    }

    // Called by TabRunOutput when Run is clicked
    internal async void StartRun(DateTime reportingMonth)
    {
        var tabFile = _tabFileSelection;
        if (tabFile.ReportingIbFiles.Count == 0 && tabFile.ReportingObFiles.Count == 0 &&
            tabFile.TransactionReportFiles.Count == 0)
        {
            MessageBox.Show("กรุณาเลือกไฟล์อย่างน้อย 1 ไฟล์ก่อนรัน",
                "ข้อมูลไม่ครบ", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        // Save latest settings
        _config = _tabSettings.GetConfig();
        ConfigManager.Save(_config);

        var p = new RunParameters
        {
            ReportingMonthInboundFiles = tabFile.ReportingIbFiles,
            ReportingMonthOutboundFiles = tabFile.ReportingObFiles,
            HistoricalInboundFiles = tabFile.HistoricalIbFiles,
            HistoricalOutboundFiles = tabFile.HistoricalObFiles,
            TransactionReportFiles = tabFile.TransactionReportFiles,
            SanctionListPath = SanctionListPath,
            CrimeListPath = CrimeListPath,
            OccupationMapPath = OccupationMapPath,
            ReportingMonth = reportingMonth,
            Config = _config,
            OutputDirectory = OutputDirectory
        };

        _cts = new CancellationTokenSource();
        var engine = new ReportEngine();
        _tabRunOutput.SetRunning(true);

        try
        {
            var (sbePath, saePath) = await engine.RunAsync(p, _tabRunOutput.AppendLog, _cts.Token);
            _tabRunOutput.SetOutputPaths(sbePath, saePath);
        }
        catch (OperationCanceledException)
        {
            _tabRunOutput.AppendLog("[ยกเลิกโดยผู้ใช้]");
        }
        catch (Exception ex)
        {
            _tabRunOutput.AppendLog($"[ERROR] {ex.Message}");
            MessageBox.Show(ex.Message, "เกิดข้อผิดพลาด", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _tabRunOutput.SetRunning(false);
            _cts?.Dispose();
            _cts = null;
        }
    }

    internal void CancelRun() => _cts?.Cancel();
}
