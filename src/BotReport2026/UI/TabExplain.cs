using BotReport2026.Engine;
using BotReport2026.Rules;

namespace BotReport2026.UI;

/// <summary>
/// Looks up one เลขที่อ้างอิงบุคคล/นิติบุคคล and shows, rule by rule, the numbers that
/// decided whether it lands in DS_SBE. Reuses the loaded <see cref="RuleContext"/> from the
/// last run; if nothing is loaded yet it reads the same input files itself.
/// </summary>
public class TabExplain : UserControl
{
    private readonly MainForm _owner;
    private TextBox _txtPersonId = null!;
    private Button _btnExplain = null!, _btnReload = null!, _btnCopy = null!;
    private Label _lblStatus = null!;
    private ProgressBar _progress = null!;
    private RichTextBox _rtb = null!;
    private bool _busy;

    private static readonly Color ColorHit = Color.FromArgb(176, 0, 32);
    private static readonly Color ColorMiss = Color.FromArgb(0, 110, 60);
    private static readonly Color ColorSkip = Color.FromArgb(120, 120, 120);
    private static readonly Color ColorInfo = Color.FromArgb(0, 90, 158);

    public TabExplain(MainForm owner)
    {
        _owner = owner;
        BuildUI();
    }

    private void BuildUI()
    {
        var main = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(8)
        };
        main.RowStyles.Add(new RowStyle(SizeType.AutoSize));   // input bar
        main.RowStyles.Add(new RowStyle(SizeType.AutoSize));   // hint
        main.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // report
        main.RowStyles.Add(new RowStyle(SizeType.AutoSize));   // status
        Controls.Add(main);

        // Row 0 — the ID box and actions
        var bar = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, Margin = new Padding(0, 0, 0, 6) };

        bar.Controls.Add(new Label
        {
            Text = "เลขที่อ้างอิงบุคคล/นิติบุคคล:",
            AutoSize = true,
            Font = new Font("Tahoma", 9.5f, FontStyle.Bold),
            Margin = new Padding(0, 6, 6, 0)
        });

        _txtPersonId = new TextBox { Width = 220, Font = new Font("Consolas", 11f) };
        _txtPersonId.KeyDown += (_, e) =>
        {
            if (e.KeyCode != Keys.Enter) return;
            e.SuppressKeyPress = true;   // no ding on Enter
            RunExplain();
        };
        bar.Controls.Add(_txtPersonId);

        _btnExplain = new Button
        {
            Text = "🔍 อธิบายการคำนวณ",
            AutoSize = true,
            BackColor = Color.FromArgb(0, 120, 215),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Margin = new Padding(8, 0, 5, 0)
        };
        _btnExplain.Click += (_, _) => RunExplain();
        bar.Controls.Add(_btnExplain);

        _btnReload = new Button { Text = "♻ โหลดข้อมูลใหม่", AutoSize = true, Margin = new Padding(5, 0, 5, 0) };
        _btnReload.Click += async (_, _) => await LoadContextAsync(force: true);
        bar.Controls.Add(_btnReload);

        _btnCopy = new Button { Text = "📋 คัดลอก", AutoSize = true, Enabled = false };
        _btnCopy.Click += (_, _) =>
        {
            if (_rtb.TextLength > 0) Clipboard.SetText(_rtb.Text);
        };
        bar.Controls.Add(_btnCopy);

        main.Controls.Add(bar, 0, 0);

        // Row 1 — hint
        main.Controls.Add(new Label
        {
            Text = "กรอกเลขบัตรประชาชน/พาสปอร์ตของลูกค้า (DS_SBE ฟิลด์ 8) แล้วกด Enter — " +
                   "ระบบจะไล่คำนวณทุก rule ทีละขั้นพร้อมตัวเลขที่ใช้ตัดสิน " +
                   "ครั้งแรกจะใช้เวลาโหลดไฟล์สักครู่",
            Dock = DockStyle.Fill,
            AutoSize = true,
            ForeColor = Color.DimGray,
            Font = new Font("Tahoma", 8.5f, FontStyle.Italic),
            Margin = new Padding(0, 0, 0, 6)
        }, 0, 1);

        // Row 2 — the explanation
        var group = new GroupBox { Text = "ผลการคำนวณ", Dock = DockStyle.Fill };
        _rtb = new RichTextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            BackColor = Color.White,
            Font = new Font("Consolas", 10f),
            WordWrap = false,
            ScrollBars = RichTextBoxScrollBars.Both
        };
        group.Controls.Add(_rtb);
        main.Controls.Add(group, 0, 2);

        // Row 3 — status + progress
        var statusBar = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, Margin = new Padding(0, 6, 0, 0) };
        _progress = new ProgressBar
        {
            Style = ProgressBarStyle.Continuous,
            Minimum = 0,
            Maximum = 100,
            Width = 240,
            Height = 20,
            Visible = false
        };
        statusBar.Controls.Add(_progress);
        _lblStatus = new Label
        {
            Text = "ยังไม่ได้โหลดข้อมูล",
            AutoSize = false,
            Width = 520,
            Height = 20,
            AutoEllipsis = true,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Color.DimGray,
            Margin = new Padding(6, 2, 0, 0)
        };
        statusBar.Controls.Add(_lblStatus);
        main.Controls.Add(statusBar, 0, 3);
    }

    /// <summary>Called by MainForm after a report run, so the tab picks up the fresh context.</summary>
    public void OnContextRefreshed()
    {
        if (InvokeRequired) { Invoke(OnContextRefreshed); return; }
        if (_owner.Engine.LastContext != null)
            SetStatus($"พร้อมใช้งาน — ข้อมูลงวด {_owner.Engine.LastContext.ReportingMonth:MMM yyyy}", Color.DarkGreen);
        else
            // The cached context was dropped (working folder changed) — say so instead of
            // leaving the old folder's "พร้อมใช้งาน" status on screen.
            SetStatus("ยังไม่ได้โหลดข้อมูล — กด \"♻ โหลดข้อมูลใหม่\" หรือรันรายงานก่อน", Color.DimGray);
    }

    private async void RunExplain()
    {
        if (_busy) return;
        string personId = _txtPersonId.Text.Trim();
        if (personId.Length == 0)
        {
            SetStatus("กรุณากรอกเลขที่อ้างอิงก่อน", Color.Firebrick);
            return;
        }

        var ctx = _owner.Engine.LastContext ?? await LoadContextAsync(force: false);
        if (ctx == null) return;

        SetStatus("กำลังคำนวณ...", Color.DimGray);
        var sections = PersonExplainer.Explain(ctx, personId);
        Render(personId, sections);
        SetStatus($"อธิบายเลข {personId} เสร็จแล้ว", Color.DarkGreen);
    }

    /// <summary>Reads the input files into a RuleContext. Returns null if it could not load.</summary>
    private async Task<RuleContext?> LoadContextAsync(bool force)
    {
        if (_busy) return null;
        if (!force && _owner.Engine.LastContext != null) return _owner.Engine.LastContext;

        var p = _owner.BuildRunParameters();
        if (p == null)
        {
            SetStatus("ยังไม่มีไฟล์ RSP ให้โหลด — ไปที่แท็บเลือกไฟล์ก่อน", Color.Firebrick);
            return null;
        }

        SetBusy(true);
        try
        {
            return await _owner.Engine.LoadContextAsync(
                p,
                _ => { },   // the log goes to the run tab; this tab shows only the stage
                pr => SetProgress(pr.Percent, pr.Stage));
        }
        catch (Exception ex)
        {
            SetStatus($"โหลดข้อมูลไม่สำเร็จ: {ex.Message}", Color.Firebrick);
            return null;
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void SetBusy(bool busy)
    {
        _busy = busy;
        _btnExplain.Enabled = !busy;
        _btnReload.Enabled = !busy;
        _progress.Visible = busy;
        if (busy) _progress.Value = 0;
    }

    private void SetProgress(int percent, string stage)
    {
        if (InvokeRequired) { Invoke(() => SetProgress(percent, stage)); return; }
        int v = Math.Clamp(percent, _progress.Minimum, _progress.Maximum);
        if (v < _progress.Maximum) { _progress.Value = v + 1; }
        _progress.Value = v;
        SetStatus($"{v}%   {stage}", Color.DimGray);
    }

    private void SetStatus(string text, Color color)
    {
        if (InvokeRequired) { Invoke(() => SetStatus(text, color)); return; }
        _lblStatus.Text = text;
        _lblStatus.ForeColor = color;
    }

    // -------------------------------------------------------------- rendering

    private void Render(string personId, List<ExplainSection> sections)
    {
        _rtb.SuspendLayout();
        _rtb.Clear();

        foreach (var s in sections)
        {
            Append($"{Marker(s.Verdict)} {s.Title}\n", VerdictColor(s.Verdict), bold: true);
            foreach (var line in s.Lines)
                Append(line + "\n", Color.Black);
            Append("\n", Color.Black);
        }

        AppendActualRunSection(personId);

        _rtb.ResumeLayout();
        _rtb.SelectionStart = 0;
        _rtb.ScrollToCaret();
        _btnCopy.Enabled = _rtb.TextLength > 0;
    }

    /// <summary>
    /// Cross-check: what the last full run actually wrote for this person. Present only
    /// after a report has been run — loading the context alone produces no DS_SBE rows.
    /// </summary>
    private void AppendActualRunSection(string personId)
    {
        var records = _owner.Engine.LastSbeRecords
            .Where(r => r.PersonRefId.Trim().Equals(personId, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (_owner.Engine.LastSbeRecords.Count == 0)
        {
            Append("ℹ ผลจริงจากการรันล่าสุด\n", ColorInfo, bold: true);
            Append("   • ยังไม่ได้รันรายงานในรอบนี้ — เทียบผลจริงไม่ได้ " +
                   "(ไปที่แท็บ 'รันและผลลัพธ์' แล้วกดรัน)\n", Color.Black);
            return;
        }

        Append("ℹ ผลจริงจากการรันล่าสุด (หลังรวมรายการซ้ำแล้ว)\n", ColorInfo, bold: true);
        if (records.Count == 0)
        {
            Append("   • ไม่มีแถว DS_SBE ของเลขนี้ในรายงานล่าสุด\n", Color.Black);
            return;
        }

        foreach (var r in records.Take(50))
            Append($"   • MTCN {r.ReferenceNo} → Rule {r.BehaviorType} ({r.RuleCode})\n", Color.Black);
        if (records.Count > 50)
            Append($"   • … อีก {records.Count - 50:N0} แถว\n", Color.Black);
        Append($"   → รวม {records.Count:N0} แถว DS_SBE\n", Color.Black);
    }

    private void Append(string text, Color color, bool bold = false)
    {
        _rtb.SelectionStart = _rtb.TextLength;
        _rtb.SelectionLength = 0;
        _rtb.SelectionColor = color;
        _rtb.SelectionFont = new Font(_rtb.Font, bold ? FontStyle.Bold : FontStyle.Regular);
        _rtb.AppendText(text);
    }

    private static string Marker(ExplainVerdict v) => v switch
    {
        ExplainVerdict.Hit => "🔴",
        ExplainVerdict.Miss => "🟢",
        ExplainVerdict.Skipped => "⚪",
        _ => "ℹ"
    };

    private static Color VerdictColor(ExplainVerdict v) => v switch
    {
        ExplainVerdict.Hit => ColorHit,
        ExplainVerdict.Miss => ColorMiss,
        ExplainVerdict.Skipped => ColorSkip,
        _ => ColorInfo
    };
}
