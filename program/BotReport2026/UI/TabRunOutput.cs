namespace BotReport2026.UI;

public class TabRunOutput : UserControl
{
    private readonly MainForm _owner;
    private DateTimePicker _dtpMonth;
    private Button _btnRun, _btnCancel, _btnOpenFolder;
    private RichTextBox _rtbLog;
    private ProgressBar _progressBar;
    private Label _lblOutputPaths;
    private string _lastOutputDir = "";

    public TabRunOutput(MainForm owner)
    {
        _owner = owner;
        BuildUI();
    }

    private void BuildUI()
    {
        var mainPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(8)
        };
        mainPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        mainPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        mainPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        mainPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(mainPanel);

        // Row 0: Month selector + Run buttons
        var topPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, Margin = new Padding(0, 0, 0, 8) };

        topPanel.Controls.Add(new Label { Text = "เดือนที่รายงาน:", AutoSize = true, TextAlign = ContentAlignment.MiddleLeft });

        _dtpMonth = new DateTimePicker
        {
            Format = DateTimePickerFormat.Custom,
            CustomFormat = "MM/yyyy",
            Value = DateTime.Today,
            Width = 120
        };
        _dtpMonth.ValueChanged += (_, _) =>
            _owner.OnReportingMonthChanged(new DateTime(_dtpMonth.Value.Year, _dtpMonth.Value.Month, 1));
        topPanel.Controls.Add(_dtpMonth);

        _btnRun = new Button
        {
            Text = "▶ รันรายงาน",
            AutoSize = true,
            BackColor = Color.FromArgb(0, 120, 215),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Margin = new Padding(20, 0, 5, 0)
        };
        _btnRun.Click += (_, _) => _owner.StartRun(new DateTime(_dtpMonth.Value.Year, _dtpMonth.Value.Month, 1));
        topPanel.Controls.Add(_btnRun);

        _btnCancel = new Button { Text = "ยกเลิก", AutoSize = true, Enabled = false };
        _btnCancel.Click += (_, _) => _owner.CancelRun();
        topPanel.Controls.Add(_btnCancel);

        mainPanel.Controls.Add(topPanel, 0, 0);

        // Row 1: Output paths label
        _lblOutputPaths = new Label
        {
            Text = "ผลลัพธ์จะถูกบันทึกที่: " + MainForm.OutputDirectory,
            Dock = DockStyle.Fill,
            AutoSize = true,
            ForeColor = Color.DimGray,
            Margin = new Padding(0, 0, 0, 8)
        };
        mainPanel.Controls.Add(_lblOutputPaths, 0, 1);

        // Row 2: Log
        var logGroup = new GroupBox { Text = "บันทึกการทำงาน", Dock = DockStyle.Fill };
        _rtbLog = new RichTextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            BackColor = Color.Black,
            ForeColor = Color.LimeGreen,
            Font = new Font("Consolas", 9.5f),
            ScrollBars = RichTextBoxScrollBars.Vertical
        };
        logGroup.Controls.Add(_rtbLog);
        mainPanel.Controls.Add(logGroup, 0, 2);

        // Row 3: Progress + Open Folder button
        var bottomPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, Margin = new Padding(0, 8, 0, 0) };

        _progressBar = new ProgressBar
        {
            Style = ProgressBarStyle.Marquee,
            Width = 400,
            Height = 22,
            Visible = false
        };
        bottomPanel.Controls.Add(_progressBar);

        _btnOpenFolder = new Button { Text = "📂 เปิดโฟลเดอร์ผลลัพธ์", AutoSize = true, Enabled = false };
        _btnOpenFolder.Click += (_, _) =>
        {
            if (Directory.Exists(_lastOutputDir))
                System.Diagnostics.Process.Start("explorer.exe", _lastOutputDir);
        };
        bottomPanel.Controls.Add(_btnOpenFolder);

        mainPanel.Controls.Add(bottomPanel, 0, 3);
    }

    public void SetReportingMonth(DateTime month)
    {
        var val = new DateTime(month.Year, month.Month, 1);
        if (val >= _dtpMonth.MinDate && val <= _dtpMonth.MaxDate)
            _dtpMonth.Value = val;
    }

    public DateTime GetReportingMonth() =>
        new DateTime(_dtpMonth.Value.Year, _dtpMonth.Value.Month, 1);

    public void AppendLog(string message)
    {
        if (InvokeRequired) { Invoke(() => AppendLog(message)); return; }
        _rtbLog.AppendText(message + "\n");
        _rtbLog.ScrollToCaret();
    }

    public void SetRunning(bool running)
    {
        if (InvokeRequired) { Invoke(() => SetRunning(running)); return; }
        _btnRun.Enabled = !running;
        _btnCancel.Enabled = running;
        _progressBar.Visible = running;
    }

    public void SetOutputPaths(string sbePath, string saePath)
    {
        if (InvokeRequired) { Invoke(() => SetOutputPaths(sbePath, saePath)); return; }
        _lastOutputDir = Path.GetDirectoryName(sbePath) ?? "";
        _lblOutputPaths.Text = $"DS_SBE: {sbePath}\nDS_SAE: {saePath}";
        _lblOutputPaths.ForeColor = Color.DarkGreen;
        _btnOpenFolder.Enabled = true;
    }
}
