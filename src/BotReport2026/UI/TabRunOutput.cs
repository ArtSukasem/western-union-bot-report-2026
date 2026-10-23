namespace BotReport2026.UI;

public class TabRunOutput : UserControl
{
    private readonly MainForm _owner;
    private DateTimePicker _dtpMonth;
    private Button _btnRun, _btnCancel, _btnOpenFolder, _btnClearResults;
    private RichTextBox _rtbLog;
    private ProgressBar _progressBar;
    private Label _lblProgress;
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
            Style = ProgressBarStyle.Continuous,
            Minimum = 0,
            Maximum = 100,
            Width = 340,
            Height = 22,
            Visible = false
        };
        bottomPanel.Controls.Add(_progressBar);

        // Fixed width: an AutoSize label would grow with the stage text and shove
        // the buttons around every time the stage changes.
        _lblProgress = new Label
        {
            Width = 330,
            Height = 22,
            AutoSize = false,
            AutoEllipsis = true,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Tahoma", 9f, FontStyle.Bold),
            ForeColor = Color.FromArgb(0, 90, 158),
            Margin = new Padding(6, 3, 12, 0),
            Visible = false
        };
        bottomPanel.Controls.Add(_lblProgress);

        _btnOpenFolder = new Button { Text = "📂 เปิดโฟลเดอร์ผลลัพธ์", AutoSize = true, Enabled = false };
        _btnOpenFolder.Click += (_, _) =>
        {
            if (Directory.Exists(_lastOutputDir))
                System.Diagnostics.Process.Start("explorer.exe", _lastOutputDir);
        };
        bottomPanel.Controls.Add(_btnOpenFolder);

        _btnClearResults = new Button
        {
            Text = "🗑 ล้างผลลัพธ์",
            AutoSize = true,
            Margin = new Padding(5, 0, 0, 0)
        };
        _btnClearResults.Click += (_, _) => ClearResults();
        bottomPanel.Controls.Add(_btnClearResults);

        mainPanel.Controls.Add(bottomPanel, 0, 3);
    }

    /// <summary>
    /// Deletes every file in the output folder. Nested folders are left alone — the
    /// engine only ever writes at the top level, so anything deeper was put there by
    /// hand and is not ours to remove.
    /// </summary>
    private void ClearResults()
    {
        string dir = MainForm.OutputDirectory;

        var files = Directory.Exists(dir) ? Directory.GetFiles(dir) : Array.Empty<string>();
        if (files.Length == 0)
        {
            MessageBox.Show($"ไม่มีไฟล์ผลลัพธ์ให้ลบ\n\n{dir}",
                "ล้างผลลัพธ์", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        // A rerun regenerates DS_SBE, but a DS_SAE workbook may already carry hand-typed
        // EDD answers, so this confirms before deleting and defaults to No.
        var confirm = MessageBox.Show(
            $"ลบไฟล์ทั้งหมด {files.Length} ไฟล์ในโฟลเดอร์ผลลัพธ์หรือไม่?\n\n{dir}\n\n" +
            "ไฟล์จะถูกลบถาวร ไม่ผ่านถังรีไซเคิล และกู้คืนไม่ได้",
            "ยืนยันการล้างผลลัพธ์",
            MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
        if (confirm != DialogResult.Yes) return;

        int deleted = 0;
        var failed = new List<string>();
        foreach (var f in files)
        {
            try
            {
                File.Delete(f);
                deleted++;
            }
            catch (Exception ex)
            {
                failed.Add($"{Path.GetFileName(f)} — {ex.Message}");
            }
        }

        AppendLog($"🗑 ล้างผลลัพธ์: ลบแล้ว {deleted} จาก {files.Length} ไฟล์");
        foreach (var f in failed) AppendLog($"  ⚠ ลบไม่สำเร็จ: {f}");

        if (failed.Count > 0)
        {
            MessageBox.Show(
                $"ลบได้ {deleted} ไฟล์ แต่ลบไม่สำเร็จ {failed.Count} ไฟล์\n" +
                "(มักเกิดจากไฟล์ยังเปิดค้างใน Excel — ปิดไฟล์แล้วลองใหม่)\n\n" +
                string.Join(Environment.NewLine, failed),
                "ล้างผลลัพธ์ไม่ครบ", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        // Nothing left to point at, so put the label back to its pre-run state.
        RefreshOutputTarget();
    }

    /// <summary>
    /// Points the label back at the results folder of the current working folder. Called on
    /// startup and whenever the working folder changes, so it never advertises a path under
    /// a folder the app has stopped using.
    /// </summary>
    public void RefreshOutputTarget()
    {
        if (InvokeRequired) { Invoke(RefreshOutputTarget); return; }
        _lastOutputDir = "";
        _lblOutputPaths.Text = "ผลลัพธ์จะถูกบันทึกที่: " + MainForm.OutputDirectory;
        _lblOutputPaths.ForeColor = Color.DimGray;
        _btnOpenFolder.Enabled = false;
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
        _btnClearResults.Enabled = !running;
        _progressBar.Visible = running;
        _lblProgress.Visible = running;
        if (running) SetProgress(0, "กำลังเริ่ม...");
    }

    /// <summary>Moves the bar to <paramref name="percent"/> (0-100) and names the stage beside it.</summary>
    public void SetProgress(int percent, string stage)
    {
        if (InvokeRequired) { Invoke(() => SetProgress(percent, stage)); return; }

        int value = Math.Clamp(percent, _progressBar.Minimum, _progressBar.Maximum);
        // Windows animates the bar towards a new value, so it lags behind the number in the
        // label. Overshooting by one and stepping back skips the animation.
        if (value < _progressBar.Maximum)
        {
            _progressBar.Value = value + 1;
            _progressBar.Value = value;
        }
        else
        {
            _progressBar.Value = value;
        }
        _lblProgress.Text = $"{value}%   {stage}";
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
