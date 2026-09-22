using BotReport2026.Readers;

namespace BotReport2026.UI;

public class TabFileSelection : UserControl
{
    private ListBox _lbReportIb = null!, _lbReportOb = null!, _lbTxnReport = null!,
                    _lbHistIb = null!, _lbHistOb = null!;
    private Label _lblReportMonth = null!;
    private Label _lblMapperStatus = null!, _lblSanctionStatus = null!, _lblCrimeStatus = null!;

    // All scanned files (not yet split)
    private List<InputFile> _allIb = new();
    private List<InputFile> _allOb = new();
    private List<InputFile> _allTxn = new();

    // Buckets after applying reporting month
    private List<InputFile> _reportingIb = new(), _reportingOb = new(), _reportingTxn = new();
    private List<InputFile> _historicalIb = new(), _historicalOb = new();

    private DateTime _currentReportingMonth = DateTime.Today;

    /// <summary>Pause before the manual reload runs, so the refresh is visible to the user.</summary>
    private const int ReloadDelayMs = 3000;

    // Getters consumed by MainForm.StartRun — return full file paths
    public List<string> ReportingIbFiles => _reportingIb.Select(f => f.Path).ToList();
    public List<string> ReportingObFiles => _reportingOb.Select(f => f.Path).ToList();
    public List<string> TransactionReportFiles => _reportingTxn.Select(f => f.Path).ToList();
    public List<string> HistoricalIbFiles => _historicalIb.Select(f => f.Path).ToList();
    public List<string> HistoricalObFiles => _historicalOb.Select(f => f.Path).ToList();

    public TabFileSelection()
    {
        BuildUI();
    }

    /// <summary>Scans the input folders and stores all files. Returns the latest month found.</summary>
    public DateTime? LoadFromFolders()
    {
        var (ib, ob) = InputFolderScanner.ScanRsp(
            MainForm.InputRspInboundDir, MainForm.InputRspOutboundDir);
        _allIb = ib;
        _allOb = ob;
        _allTxn = InputFolderScanner.ScanTxnReport(MainForm.InputTxnReportDir);

        var months = _allIb.Concat(_allOb).Concat(_allTxn)
            .Where(f => f.Month.HasValue)
            .Select(f => f.Month!.Value)
            .ToList();

        return months.Count > 0 ? months.Max() : (DateTime?)null;
    }

    /// <summary>Re-buckets the scanned files based on the selected reporting month and refreshes the UI.</summary>
    public void ApplyReportingMonth(DateTime month)
    {
        _currentReportingMonth = new DateTime(month.Year, month.Month, 1);

        bool IsReporting(InputFile f) =>
            f.Month.HasValue &&
            f.Month.Value.Year == _currentReportingMonth.Year &&
            f.Month.Value.Month == _currentReportingMonth.Month;

        bool IsHistorical(InputFile f) =>
            // Earlier months OR files whose month could not be parsed (conservative)
            !f.Month.HasValue || f.Month.Value < _currentReportingMonth;

        _reportingIb = _allIb.Where(IsReporting).ToList();
        _reportingOb = _allOb.Where(IsReporting).ToList();
        _reportingTxn = _allTxn.Where(IsReporting).ToList();
        _historicalIb = _allIb.Where(IsHistorical).ToList();
        _historicalOb = _allOb.Where(IsHistorical).ToList();

        RefreshLists();
    }

    private void RefreshLists()
    {
        _lblReportMonth.Text = $"เดือนที่รายงาน (auto): {_currentReportingMonth:MMM yyyy}";

        RefreshReferenceStatus();

        FillListBox(_lbReportIb, _reportingIb);
        FillListBox(_lbReportOb, _reportingOb);
        FillListBox(_lbTxnReport, _reportingTxn);
        FillListBox(_lbHistIb, _historicalIb);
        FillListBox(_lbHistOb, _historicalOb);
    }

    /// <summary>Checks existence of mapper + sanction list and updates the ✅/❌ status labels.</summary>
    public void RefreshReferenceStatus()
    {
        SetStatus(_lblMapperStatus, "Mapper (อาชีพ/รายได้)", MainForm.OccupationMapPath);
        SetStatus(_lblSanctionStatus, "Sanction List (CFR/UN/TH)", MainForm.SanctionListPath);
        SetStatus(_lblCrimeStatus, "อาชญากรรม/หน้าร้านน่าสงสัย (Rule 212, 301)", MainForm.CrimeListPath);
    }

    private static void SetStatus(Label lbl, string title, string path)
    {
        bool exists = File.Exists(path);
        string fileName = Path.GetFileName(path);
        if (exists)
        {
            lbl.Text = $"✅  {title} : {fileName}";
            lbl.ForeColor = Color.FromArgb(0, 128, 0);
        }
        else
        {
            lbl.Text = $"❌  {title} : ไม่พบไฟล์ ({fileName})";
            lbl.ForeColor = Color.FromArgb(192, 0, 0);
        }
    }

    private static Label MakeStatusLabel() =>
        new Label
        {
            AutoSize = true,
            Font = new Font("Tahoma", 9.5f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(4, 8, 12, 4)
        };

    private static void FillListBox(ListBox lb, List<InputFile> files)
    {
        lb.BeginUpdate();
        lb.Items.Clear();
        if (files.Count == 0)
            lb.Items.Add("(ไม่พบไฟล์)");
        else
            foreach (var f in files.OrderBy(f => f.FileName))
                lb.Items.Add(f.Display);
        lb.EndUpdate();
    }

    private void BuildUI()
    {
        AutoScroll = true;
        // Docked Top (not Fill) so the stack keeps its natural height and AutoScroll
        // gives a vertical scroll bar; with Fill the bottom group was squeezed off-screen.
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 1,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink
        };
        Controls.Add(layout);

        // Top bar: reporting month label + action buttons
        var topBar = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            Height = 38,
            AutoSize = true,
            Padding = new Padding(4)
        };
        _lblReportMonth = new Label
        {
            Text = "เดือนที่รายงาน (auto): -",
            AutoSize = true,
            Font = new Font("Tahoma", 10f, FontStyle.Bold),
            ForeColor = Color.FromArgb(0, 90, 158),
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(4, 6, 20, 0)
        };
        var btnReload = new Button { Text = "🔄 โหลดใหม่", AutoSize = true };
        var btnOpenRspIb = new Button { Text = "📂 เปิดโฟลเดอร์ RSP Inbound", AutoSize = true };
        var btnOpenRspOb = new Button { Text = "📂 เปิดโฟลเดอร์ RSP Outbound", AutoSize = true };
        var btnOpenTxn = new Button { Text = "📂 เปิดโฟลเดอร์ Transaction Report", AutoSize = true };

        btnReload.Click += async (_, _) =>
        {
            // Short pause so the user sees the reload actually happen, and so files
            // that were just dropped into the input folders finish being written.
            var originalText = btnReload.Text;
            btnReload.Enabled = false;
            btnReload.Text = "⏳ กำลังโหลด...";
            Cursor = Cursors.WaitCursor;
            try
            {
                await Task.Delay(ReloadDelayMs);
                LoadFromFolders();
                ApplyReportingMonth(_currentReportingMonth);
            }
            finally
            {
                Cursor = Cursors.Default;
                btnReload.Text = originalText;
                btnReload.Enabled = true;
            }
        };
        btnOpenRspIb.Click += (_, _) => OpenFolder(MainForm.InputRspInboundDir);
        btnOpenRspOb.Click += (_, _) => OpenFolder(MainForm.InputRspOutboundDir);
        btnOpenTxn.Click += (_, _) => OpenFolder(MainForm.InputTxnReportDir);

        topBar.Controls.AddRange(new Control[]
            { _lblReportMonth, btnReload, btnOpenRspIb, btnOpenRspOb, btnOpenTxn });
        layout.Controls.Add(topBar);

        var hint = new Label
        {
            Text = "วางไฟล์ในโฟลเดอร์ย่อย input-rsp-inbound/, input-rsp-outbound/ และ input-transaction-report/ " +
                   "ภายใต้โฟลเดอร์ทำงานที่เลือกไว้ด้านบน โปรแกรมจะโหลดและแยกเดือนให้อัตโนมัติ " +
                   "(ชื่อไฟล์ต้องมีเดือน เช่น May26)",
            Dock = DockStyle.Fill,
            AutoSize = true,
            ForeColor = Color.DimGray,
            Font = new Font("Tahoma", 8.5f, FontStyle.Italic),
            Margin = new Padding(4, 0, 0, 6)
        };
        layout.Controls.Add(hint);

        // Reference files status (mapper + lookups)
        var grpRef = new GroupBox
        {
            Text = "ไฟล์อ้างอิง (จำเป็นต้องมีก่อนรัน)",
            Dock = DockStyle.Fill,
            Height = 160,
            Padding = new Padding(8)
        };
        layout.Controls.Add(grpRef);

        // Scroll host: lets the status label + button columns keep their natural
        // width and scroll horizontally instead of being clipped on a narrow window.
        var refScroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
        grpRef.Controls.Add(refScroll);

        var tlRef = new TableLayoutPanel
        {
            Location = new Point(0, 0),
            ColumnCount = 2,
            RowCount = 3,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink
        };
        tlRef.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        tlRef.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        for (int i = 0; i < 3; i++)
            tlRef.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        refScroll.Controls.Add(tlRef);

        _lblMapperStatus = MakeStatusLabel();
        _lblSanctionStatus = MakeStatusLabel();
        _lblCrimeStatus = MakeStatusLabel();

        var btnOpenMapper = new Button { Text = "📂 เปิดโฟลเดอร์ mapper", AutoSize = true, Anchor = AnchorStyles.Left };
        var btnOpenLookups = new Button { Text = "📂 เปิดโฟลเดอร์ lookups", AutoSize = true, Anchor = AnchorStyles.Left };
        var btnOpenLookups2 = new Button { Text = "📂 เปิดโฟลเดอร์ lookups", AutoSize = true, Anchor = AnchorStyles.Left };
        btnOpenMapper.Click += (_, _) => OpenFolder(Path.GetDirectoryName(MainForm.OccupationMapPath)!);
        btnOpenLookups.Click += (_, _) => OpenFolder(Path.GetDirectoryName(MainForm.SanctionListPath)!);
        btnOpenLookups2.Click += (_, _) => OpenFolder(Path.GetDirectoryName(MainForm.CrimeListPath)!);

        tlRef.Controls.Add(_lblMapperStatus, 0, 0);
        tlRef.Controls.Add(btnOpenMapper, 1, 0);
        tlRef.Controls.Add(_lblSanctionStatus, 0, 1);
        tlRef.Controls.Add(btnOpenLookups, 1, 1);
        tlRef.Controls.Add(_lblCrimeStatus, 0, 2);
        tlRef.Controls.Add(btnOpenLookups2, 1, 2);

        // RSP reporting month
        var grpReport = new GroupBox
        {
            Text = "ไฟล์ RSP เดือนที่รายงาน",
            Dock = DockStyle.Fill,
            Height = 200,
            Padding = new Padding(8)
        };
        layout.Controls.Add(grpReport);

        var tlReport = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        tlReport.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        tlReport.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        grpReport.Controls.Add(tlReport);

        _lbReportIb = MakeListBox();
        _lbReportOb = MakeListBox();
        tlReport.Controls.Add(MakeFilePanel("RSP Inbound (รับเงิน)", _lbReportIb), 0, 0);
        tlReport.Controls.Add(MakeFilePanel("RSP Outbound (ส่งเงิน)", _lbReportOb), 1, 0);

        // Transaction Report
        var grpTxn = new GroupBox
        {
            Text = "ไฟล์ Transaction Report (Online) — เดือนที่รายงาน",
            Dock = DockStyle.Fill,
            Height = 140,
            Padding = new Padding(8)
        };
        layout.Controls.Add(grpTxn);

        _lbTxnReport = MakeListBox();
        grpTxn.Controls.Add(MakeFilePanel("Transaction Report", _lbTxnReport));

        // Historical RSP
        var grpHist = new GroupBox
        {
            Text = "ไฟล์ RSP ย้อนหลัง (สำหรับ Rule 202 & 203) — เดือนก่อนหน้า",
            Dock = DockStyle.Fill,
            Height = 200,
            Padding = new Padding(8)
        };
        layout.Controls.Add(grpHist);

        var tlHist = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        tlHist.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        tlHist.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        grpHist.Controls.Add(tlHist);

        _lbHistIb = MakeListBox();
        _lbHistOb = MakeListBox();
        tlHist.Controls.Add(MakeFilePanel("RSP Inbound ย้อนหลัง", _lbHistIb), 0, 0);
        tlHist.Controls.Add(MakeFilePanel("RSP Outbound ย้อนหลัง", _lbHistOb), 1, 0);
    }

    private static void OpenFolder(string dir)
    {
        try
        {
            Directory.CreateDirectory(dir);
            System.Diagnostics.Process.Start("explorer.exe", dir);
        }
        catch { }
    }

    private static ListBox MakeListBox() =>
        new ListBox
        {
            Dock = DockStyle.Fill,
            HorizontalScrollbar = true,
            SelectionMode = SelectionMode.None
        };

    private Panel MakeFilePanel(string title, ListBox lb)
    {
        var panel = new Panel { Dock = DockStyle.Fill };
        var lbl = new Label
        {
            Text = title,
            Dock = DockStyle.Top,
            Font = new Font("Tahoma", 9f, FontStyle.Bold),
            Height = 20
        };
        panel.Controls.Add(lb);
        panel.Controls.Add(lbl);
        return panel;
    }
}
