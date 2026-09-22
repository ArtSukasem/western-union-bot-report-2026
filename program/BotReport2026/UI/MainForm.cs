using BotReport2026.Config;
using BotReport2026.Engine;
using BotReport2026.Models;

namespace BotReport2026.UI;

public partial class MainForm : Form
{
    private AppConfig _config = ConfigManager.Load();
    private CancellationTokenSource? _cts;

    /// <summary>One engine for the app's lifetime: it caches the loaded RuleContext and the
    /// last run's DS_SBE rows, which the ตรวจสอบรายบุคคล tab reads.</summary>
    private readonly ReportEngine _engine = new();

    internal ReportEngine Engine => _engine;

    // All paths hang off the working folder the user picked in the WorkspaceBar —
    // see Workspace for how the root is resolved and persisted.
    public static string SanctionListPath => Workspace.SanctionListPath;
    public static string CrimeListPath => Workspace.CrimeListPath;
    public static string OccupationMapPath => Workspace.OccupationMapPath;
    /// <summary>DS_SBE/DS_SAE reference tables, produced by tools/extract_v3_lookups.py.</summary>
    public static string ReferenceDataDirectory => Workspace.ReferenceDataDirectory;
    public static string OutputDirectory => Workspace.OutputDirectory;
    public static string InputRspInboundDir => Workspace.InputRspInboundDir;
    public static string InputRspOutboundDir => Workspace.InputRspOutboundDir;
    public static string InputTxnReportDir => Workspace.InputTxnReportDir;

    /// <summary>Creates all input/lookup/output folders so users have a place to drop files.</summary>
    public static void EnsureFolders() => Workspace.EnsureFolders();

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

        // Auto-load input files from the working folder on startup
        EnsureFolders();
        _workspaceBar.RootChanged += ApplyWorkspaceChange;
        ReloadWorkspaceFiles();
    }

    /// <summary>
    /// Rescans the working folder's input sub-folders, moves the reporting month to the
    /// latest month found and refreshes every folder-dependent label.
    /// </summary>
    private void ReloadWorkspaceFiles()
    {
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
        _tabRunOutput.RefreshOutputTarget();
    }

    /// <summary>
    /// Re-points the app after the user picked a different working folder. The loaded
    /// context belongs to the old folder, so it is dropped; reference files (lookups,
    /// mapper, SAE-lookups) are per-install rather than per-month, so a folder that has
    /// none of them offers to copy them over from the folder in use before.
    /// </summary>
    internal void ApplyWorkspaceChange(string previousRoot)
    {
        if (Workspace.IsMissingReferenceFiles() && Workspace.HasReferenceFiles(previousRoot))
        {
            var copy = MessageBox.Show(
                "โฟลเดอร์ทำงานใหม่ยังไม่มีไฟล์อ้างอิง (lookups, mapper, SAE-lookups)\n\n" +
                $"คัดลอกจากโฟลเดอร์เดิมหรือไม่?\n{previousRoot}",
                "คัดลอกไฟล์อ้างอิง", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (copy == DialogResult.Yes)
            {
                int copied = Workspace.CopyReferenceFiles(previousRoot);
                _tabRunOutput.AppendLog($"📁 คัดลอกไฟล์อ้างอิง {copied} ไฟล์จาก {previousRoot}");
            }
        }

        _engine.ResetContext();
        _tabExplain.OnContextRefreshed();
        ReloadWorkspaceFiles();
        _tabRunOutput.AppendLog($"📁 เปลี่ยนโฟลเดอร์ทำงานเป็น: {Workspace.Root}");
    }

    /// <summary>Called when the reporting-month picker changes — re-split files by month.</summary>
    internal void OnReportingMonthChanged(DateTime month)
    {
        _tabFileSelection.ApplyReportingMonth(month);
    }

    /// <summary>
    /// Collects the selected files, current settings and reporting month into RunParameters.
    /// Returns null when no input file is selected. Shared by the run button and the
    /// ตรวจสอบรายบุคคล tab, which loads the same inputs without writing a report.
    /// </summary>
    internal RunParameters? BuildRunParameters(DateTime? reportingMonth = null)
    {
        var tabFile = _tabFileSelection;
        if (tabFile.ReportingIbFiles.Count == 0 && tabFile.ReportingObFiles.Count == 0 &&
            tabFile.TransactionReportFiles.Count == 0)
            return null;

        // Save latest settings
        _config = _tabSettings.GetConfig();
        ConfigManager.Save(_config);

        return new RunParameters
        {
            ReportingMonthInboundFiles = tabFile.ReportingIbFiles,
            ReportingMonthOutboundFiles = tabFile.ReportingObFiles,
            HistoricalInboundFiles = tabFile.HistoricalIbFiles,
            HistoricalOutboundFiles = tabFile.HistoricalObFiles,
            TransactionReportFiles = tabFile.TransactionReportFiles,
            SanctionListPath = SanctionListPath,
            CrimeListPath = CrimeListPath,
            OccupationMapPath = OccupationMapPath,
            ReferenceDataDirectory = ReferenceDataDirectory,
            ReportingMonth = reportingMonth ?? _tabRunOutput.GetReportingMonth(),
            Config = _config,
            OutputDirectory = OutputDirectory
        };
    }

    // Called by TabRunOutput when Run is clicked
    internal async void StartRun(DateTime reportingMonth)
    {
        var p = BuildRunParameters(reportingMonth);
        if (p == null)
        {
            MessageBox.Show("กรุณาเลือกไฟล์อย่างน้อย 1 ไฟล์ก่อนรัน",
                "ข้อมูลไม่ครบ", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _cts = new CancellationTokenSource();
        _tabRunOutput.SetRunning(true);

        try
        {
            var (sbePath, saePath) = await _engine.RunAsync(
                p, _tabRunOutput.AppendLog,
                pr => _tabRunOutput.SetProgress(pr.Percent, pr.Stage), _cts.Token);
            _tabRunOutput.SetOutputPaths(sbePath, saePath);
            _tabExplain.OnContextRefreshed();
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
