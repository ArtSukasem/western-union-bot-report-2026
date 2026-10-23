using BotReport2026.Models;

namespace BotReport2026.UI;

public class TabSettings : UserControl
{
    // RSP input
    private TextBox _tbRspStatuses;
    // DS_SAE
    private TextBox _tbSaeOnlineBranches;
    // Rule 202
    private NumericUpDown _n202Months, _n202Mult;
    // Rule 203
    private NumericUpDown _n203Months, _n203Mult;
    private TextBox _tb203Branches;
    // Rule 206
    private TextBox _tb206Branches;
    private NumericUpDown _n206Principal, _n206Count;
    // Rule 208
    private NumericUpDown _n208Sub, _n208DayCount, _n208DayAmt, _n208WkCount, _n208WkAmt, _n208MoCount, _n208MoAmt;
    // Rule 209
    private NumericUpDown _n209Hours;

    public TabSettings()
    {
        AutoScroll = true;
        var layout = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true
        };
        Controls.Add(layout);

        layout.Controls.Add(MakeRspInput());
        layout.Controls.Add(MakeSaeInput());
        layout.Controls.Add(Make101Info());
        layout.Controls.Add(Make202());
        layout.Controls.Add(Make203());
        layout.Controls.Add(Make206());
        layout.Controls.Add(Make208());
        layout.Controls.Add(Make209());
        layout.Controls.Add(Make212Info());
        layout.Controls.Add(Make301Info());

        var btnPanel = new FlowLayoutPanel { Height = 40, AutoSize = true };
        var btnSave = new Button { Text = "💾 บันทึกตั้งค่า", AutoSize = true };
        var btnLoad = new Button { Text = "📂 โหลดตั้งค่า", AutoSize = true };
        btnSave.Click += (_, _) => { Config.ConfigManager.Save(GetConfig()); MessageBox.Show("บันทึกแล้ว"); };
        btnLoad.Click += (_, _) => SetConfig(Config.ConfigManager.Load());
        btnPanel.Controls.AddRange(new Control[] { btnSave, btnLoad });
        layout.Controls.Add(btnPanel);
    }

    private GroupBox MakeRspInput()
    {
        var gb = MakeGroup("ไฟล์ RSP (.csv) — สถานะรายการที่นำมาคำนวณ (ใช้กับทุก rule)");
        AddRow(gb, "สถานะที่นับ (cl2) — บรรทัดละ 1, เว้นว่าง = ไม่กรอง:",
            _tbRspStatuses = MakeTextBox("PAID\r\nUNPAID"), labelTop: true);
        return gb;
    }

    private GroupBox MakeSaeInput()
    {
        var gb = MakeGroup("DS_SAE — สาขาที่ถือเป็นช่องทาง Online (330004, ไม่มีสถานที่ทำธุรกรรม)");
        AddRow(gb, "รหัสสาขา Online — บรรทัดละ 1:",
            _tbSaeOnlineBranches = MakeTextBox("ATH170025\r\nATH170014"), labelTop: true);
        return gb;
    }

    private GroupBox Make101Info()
    {
        return MakeGroup("Rule 101 — ตรงกับ Sanction / CFR List (depends on: saction_list.xlsx)");
    }

    private GroupBox Make202()
    {
        var gb = MakeGroup("Rule 202 — ยอดรวมผิดปกติ (Rolling Average) (depends on: RSP)");
        AddRow(gb, "จำนวนเดือนย้อนหลัง:", _n202Months = MakeNum(1, 24, 6));
        AddRow(gb, "ตัวคูณ (เท่า):", _n202Mult = MakeNum(1m, 20m, 3m, 1));
        return gb;
    }

    private GroupBox Make203()
    {
        var gb = MakeGroup("Rule 203 — ไม่สอดคล้องกับ Profile รายได้/อาชีพ (depends on: RSP, Transaction Report, mapper)");
        AddRow(gb, "จำนวนเดือนย้อนหลัง (Retail):", _n203Months = MakeNum(1, 24, 3));
        AddRow(gb, "ตัวคูณ (เท่า):", _n203Mult = MakeNum(1m, 20m, 3m, 1));
        AddRow(gb, "สาขาที่ยกเว้น (Online) — บรรทัดละ 1:", _tb203Branches = MakeTextBox("ATH170025"), labelTop: true);
        return gb;
    }

    private GroupBox Make206()
    {
        var gb = MakeGroup("Rule 206 — สาขาพื้นที่เสี่ยงตามชายแดน (depends on: RSP)");
        AddRow(gb, "รหัสสาขาพื้นที่เสี่ยง — บรรทัดละ 1:", _tb206Branches = MakeTextBox(""), labelTop: true);
        AddRow(gb, "เกณฑ์ยอดรวมต่อเดือน (บาท):", _n206Principal = MakeNum(0m, 100000000m, 700000m, 0));
        AddRow(gb, "เกณฑ์จำนวนครั้งต่อเดือน:", _n206Count = MakeNum(1, 1000, 20));
        return gb;
    }

    private GroupBox Make208()
    {
        var gb = MakeGroup("Rule 208 — Structuring (แบ่งยอดเงินหลีกเลี่ยงการรายงาน) (depends on: RSP)");
        AddRow(gb, "เกณฑ์ยอดต่อรายการ < (บาท):", _n208Sub = MakeNum(1m, 10000000m, 50000m, 0));
        AddRow(gb, "จำนวนครั้ง/วัน >", _n208DayCount = MakeNum(1, 1000, 10));
        AddRow(gb, "ยอดรวม/วัน ≥ (บาท):", _n208DayAmt = MakeNum(1m, 100000000m, 150000m, 0));
        AddRow(gb, "จำนวนครั้ง/สัปดาห์ >", _n208WkCount = MakeNum(1, 1000, 20));
        AddRow(gb, "ยอดรวม/สัปดาห์ ≥ (บาท):", _n208WkAmt = MakeNum(1m, 100000000m, 150000m, 0));
        AddRow(gb, "จำนวนครั้ง/เดือน >", _n208MoCount = MakeNum(1, 1000, 50));
        AddRow(gb, "ยอดรวม/เดือน ≥ (บาท):", _n208MoAmt = MakeNum(1m, 100000000m, 150000m, 0));
        return gb;
    }

    private GroupBox Make209()
    {
        var gb = MakeGroup("Rule 209 — ทำธุรกรรมต่อเนื่อง 24/7 (depends on: RSP)");
        AddRow(gb, "จำนวนชั่วโมงต่อเนื่อง ≥:", _n209Hours = MakeNum(1, 24, 16));
        return gb;
    }

    private GroupBox Make212Info()
    {
        return MakeGroup("Rule 212 — บุคคลอาชญากรรมทางการเงิน (depends on: RSP, financial_crime_individuals_...xlsx)");
    }

    private GroupBox Make301Info()
    {
        return MakeGroup("Rule 301 — พฤติกรรมลูกค้าหน้าร้านน่าสงสัย (depends on: RSP, financial_crime_individuals_...xlsx)");
    }

    public AppConfig GetConfig()
    {
        return new AppConfig
        {
            RspIncludedStatuses = SplitLines(_tbRspStatuses.Text),
            SaeOnlineChannelBranches = SplitLines(_tbSaeOnlineBranches.Text),
            Rule202PriorMonths = (int)_n202Months.Value,
            Rule202Multiplier = _n202Mult.Value,
            Rule203RollingMonths = (int)_n203Months.Value,
            Rule203Multiplier = _n203Mult.Value,
            Rule203ExcludedBranches = SplitLines(_tb203Branches.Text),
            Rule206HighRiskBranches = SplitLines(_tb206Branches.Text),
            Rule206PrincipalThreshold = _n206Principal.Value,
            Rule206TxnCountThreshold = (int)_n206Count.Value,
            Rule208SubThreshold = _n208Sub.Value,
            Rule208DailyCount = (int)_n208DayCount.Value,
            Rule208DailyAmount = _n208DayAmt.Value,
            Rule208WeeklyCount = (int)_n208WkCount.Value,
            Rule208WeeklyAmount = _n208WkAmt.Value,
            Rule208MonthlyCount = (int)_n208MoCount.Value,
            Rule208MonthlyAmount = _n208MoAmt.Value,
            Rule209ConsecutiveHours = (int)_n209Hours.Value
        };
    }

    public void SetConfig(AppConfig c)
    {
        _tbRspStatuses.Text = string.Join(Environment.NewLine, c.RspIncludedStatuses ?? new());
        _tbSaeOnlineBranches.Text = string.Join(Environment.NewLine, c.SaeOnlineChannelBranches ?? new());
        _n202Months.Value = c.Rule202PriorMonths;
        _n202Mult.Value = c.Rule202Multiplier;
        _n203Months.Value = c.Rule203RollingMonths;
        _n203Mult.Value = c.Rule203Multiplier;
        _tb203Branches.Text = string.Join(Environment.NewLine, c.Rule203ExcludedBranches ?? new());
        _tb206Branches.Text = string.Join(Environment.NewLine, c.Rule206HighRiskBranches ?? new());
        _n206Principal.Value = c.Rule206PrincipalThreshold;
        _n206Count.Value = c.Rule206TxnCountThreshold;
        _n208Sub.Value = c.Rule208SubThreshold;
        _n208DayCount.Value = c.Rule208DailyCount;
        _n208DayAmt.Value = c.Rule208DailyAmount;
        _n208WkCount.Value = c.Rule208WeeklyCount;
        _n208WkAmt.Value = c.Rule208WeeklyAmount;
        _n208MoCount.Value = c.Rule208MonthlyCount;
        _n208MoAmt.Value = c.Rule208MonthlyAmount;
        _n209Hours.Value = c.Rule209ConsecutiveHours;
    }

    // --- Helpers ---
    private static GroupBox MakeGroup(string title)
    {
        var gb = new GroupBox
        {
            Text = title,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(10, 18, 10, 10),
            Margin = new Padding(0, 0, 0, 10),
            // Lock width so AutoSize only grows height — prevents the title from
            // collapsing to one-character-per-line (esp. for groups with no rows).
            MinimumSize = new Size(840, 0),
            MaximumSize = new Size(840, 0)
        };

        // Inner flow panel so rows stack vertically instead of overlapping at (0,0)
        var inner = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Top
        };
        gb.Controls.Add(inner);
        gb.Tag = inner;
        return gb;
    }

    private static FlowLayoutPanel Inner(GroupBox gb) => (FlowLayoutPanel)gb.Tag!;

    private static NumericUpDown MakeNum(decimal min, decimal max, decimal val, int decimals = 0)
    {
        return new NumericUpDown
        {
            Minimum = min, Maximum = max, Value = val,
            DecimalPlaces = decimals,
            Width = 120, Margin = new Padding(0, 0, 20, 0)
        };
    }

    private static NumericUpDown MakeNum(int min, int max, int val) =>
        MakeNum(min, max, val, 0);

    private static TextBox MakeTextBox(string defaultText) =>
        new TextBox
        {
            Text = defaultText,
            Multiline = true,
            Height = 60,
            Width = 300,
            ScrollBars = ScrollBars.Vertical
        };

    private static void AddRow(GroupBox gb, string label, Control ctrl, bool labelTop = false)
    {
        var panel = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = labelTop ? FlowDirection.TopDown : FlowDirection.LeftToRight,
            Margin = new Padding(0, 0, 0, 6)
        };
        panel.Controls.Add(new Label
        {
            Text = label,
            AutoSize = true,
            TextAlign = ContentAlignment.MiddleLeft
        });
        panel.Controls.Add(ctrl);
        Inner(gb).Controls.Add(panel);
    }

    private static List<string> SplitLines(string text) =>
        text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => s.Length > 0)
            .ToList();
}
