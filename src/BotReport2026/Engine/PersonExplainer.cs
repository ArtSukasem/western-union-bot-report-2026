using System.Globalization;
using BotReport2026.Models;
using BotReport2026.Readers;
using BotReport2026.Rules;

namespace BotReport2026.Engine;

public enum ExplainVerdict
{
    /// <summary>Plain information, no pass/fail (the profile block).</summary>
    Info,
    /// <summary>The person meets this rule's criteria.</summary>
    Hit,
    /// <summary>Criteria evaluated and not met.</summary>
    Miss,
    /// <summary>Rule not evaluated — no list configured, no data to test against.</summary>
    Skipped
}

/// <summary>One block of the explanation: a rule, its verdict, and the numbers behind it.</summary>
public sealed class ExplainSection
{
    public string Title { get; init; } = "";
    public ExplainVerdict Verdict { get; init; }
    public List<string> Lines { get; } = new();
}

/// <summary>
/// Re-walks every rule for a single เลขที่อ้างอิงบุคคล and shows the working: the inputs
/// read, the threshold computed, and the comparison that decided it. The arithmetic here
/// mirrors <see cref="Rule101SanctionMatch"/> … <see cref="Rule301SuspiciousRetail"/> —
/// when a rule's criteria change, change its Explain method here too.
/// </summary>
public static class PersonExplainer
{
    private const string Bullet = "   • ";
    private const string Arrow = "   → ";

    private static string Money(decimal v) => v.ToString("N2", CultureInfo.InvariantCulture);
    private static string Ym((int year, int month) ym) => $"{ym.year}-{ym.month:00}";

    /// <summary>Rule order used by ReportEngine — the first hit wins a shared MTCN.</summary>
    private static readonly string[] RulePriority =
        { "101", "202", "203", "206", "208", "209", "212", "301" };

    public static List<ExplainSection> Explain(RuleContext ctx, string rawPersonId)
    {
        var sections = new List<ExplainSection>();
        string personId = (rawPersonId ?? "").Trim();

        if (personId.Length == 0)
        {
            sections.Add(Section("ไม่ได้ระบุเลขที่อ้างอิง", ExplainVerdict.Skipped,
                "กรุณากรอกเลขที่อ้างอิงบุคคล/นิติบุคคล (เลขบัตรประชาชน/พาสปอร์ต) แล้วกดอธิบาย"));
            return sections;
        }

        bool Same(string? id) => !string.IsNullOrWhiteSpace(id) &&
                                 id.Trim().Equals(personId, StringComparison.OrdinalIgnoreCase);

        var reporting = ctx.ReportingMonthRsp.Where(t => Same(t.PersonId)).ToList();
        var historical = ctx.HistoricalRsp.Where(t => Same(t.PersonId)).ToList();
        var online = ctx.TransactionReport.Where(r => Same(r.SenderIdNumber)).ToList();

        sections.Add(Profile(ctx, personId, reporting, historical, online));

        if (reporting.Count == 0 && historical.Count == 0 && online.Count == 0)
        {
            sections.Add(Section("หยุดการตรวจ", ExplainVerdict.Skipped,
                "ไม่พบธุรกรรมของเลขที่อ้างอิงนี้ในไฟล์ที่โหลดไว้ จึงไม่มีอะไรให้คำนวณ",
                "ตรวจสอบว่า: (1) พิมพ์เลขถูกต้อง (2) เลือกเดือนที่รายงานถูกต้อง " +
                "(3) ไฟล์ RSP ของเดือนนั้นอยู่ในโฟลเดอร์แล้ว"));
            return sections;
        }

        sections.Add(Rule101(ctx, personId, reporting));
        sections.Add(Rule202(ctx, personId, reporting, historical));
        sections.Add(Rule203Online(ctx, online));
        sections.Add(Rule203Retail(ctx, personId, reporting, historical));
        sections.Add(Rule206(ctx, reporting));
        sections.Add(Rule208(ctx, reporting));
        sections.Add(Rule209(ctx, reporting));
        sections.Add(Rule212(ctx, reporting));
        sections.Add(Rule301(ctx, personId, reporting));
        sections.Add(Summary(sections));

        return sections;
    }

    // ---------------------------------------------------------------- profile

    private static ExplainSection Profile(
        RuleContext ctx, string personId,
        List<RspTransaction> reporting, List<RspTransaction> historical, List<TransactionReportRow> online)
    {
        var s = Section($"ข้อมูลบุคคล — {personId}", ExplainVerdict.Info);
        var any = reporting.Concat(historical).FirstOrDefault();

        s.Lines.Add($"{Bullet}งวดข้อมูล : {ctx.ReportingMonth:MMM yyyy} (สิ้นงวด {ctx.DataPeriod})");
        s.Lines.Add($"{Bullet}ชื่อ-สกุล : {(any?.FullName is { Length: > 0 } n ? n : "-")}");
        s.Lines.Add($"{Bullet}อาชีพ (cl54/cl89) : " +
                    $"{(reporting.Concat(historical).FirstOrDefault(t => !string.IsNullOrWhiteSpace(t.Occupation))?.Occupation ?? "-")}");

        var ib = reporting.Where(t => t.Direction == "IB").ToList();
        var ob = reporting.Where(t => t.Direction == "OB").ToList();
        s.Lines.Add($"{Bullet}เดือนที่รายงาน : IB {ib.Count:N0} รายการ รวม {Money(ib.Sum(t => t.Principal))} บาท | " +
                    $"OB {ob.Count:N0} รายการ รวม {Money(ob.Sum(t => t.Principal))} บาท");

        var branches = reporting.Select(t => t.BranchAccountId).Where(b => b.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(b => b).ToList();
        s.Lines.Add($"{Bullet}สาขาที่ใช้ : {(branches.Count > 0 ? string.Join(", ", branches) : "-")}");

        var histMonths = historical
            .Where(t => t.TransactionDate.HasValue)
            .GroupBy(t => (t.TransactionDate!.Value.Year, t.TransactionDate.Value.Month))
            .OrderBy(g => g.Key.Year * 100 + g.Key.Month)
            .Select(g => $"{Ym(g.Key)}={Money(g.Sum(t => t.Principal))}")
            .ToList();
        s.Lines.Add($"{Bullet}ย้อนหลัง : {(histMonths.Count > 0 ? string.Join("  ", histMonths) : "ไม่มีข้อมูล")}");
        s.Lines.Add($"{Bullet}Transaction Report (online) ที่เข้าเกณฑ์ 203 : {online.Count:N0} รายการ");
        return s;
    }

    // ------------------------------------------------------------------- 101

    private static ExplainSection Rule101(RuleContext ctx, string personId, List<RspTransaction> reporting)
    {
        var s = Section("Rule 101 — ตรงกับ Sanction / CFR List", ExplainVerdict.Miss);

        bool thHit = ctx.SanctionData.ThSanctionIds.Contains(personId);
        s.Lines.Add($"{Bullet}ขั้นที่ 1 — ค้นเลขในรายชื่อ TH/CFR ({ctx.SanctionData.ThSanctionIds.Count:N0} เลข) : " +
                    (thHit ? "พบ" : "ไม่พบ"));

        bool unIdHit = ctx.SanctionData.UnSanctionIds.Contains(personId);
        s.Lines.Add($"{Bullet}ขั้นที่ 2 — ค้นเลขในรายชื่อ UN ({ctx.SanctionData.UnSanctionIds.Count:N0} เลข) : " +
                    (unIdHit ? "พบ" : "ไม่พบ"));

        bool unHit = false;
        if (!thHit && unIdHit)
        {
            string fullName = (reporting.FirstOrDefault()?.FullName ?? "").ToUpperInvariant();
            s.Lines.Add($"{Bullet}ขั้นที่ 3 — UN ต้องตรงทั้งเลขและชื่อ; ชื่อใน RSP = \"{fullName}\"");
            if (ctx.SanctionData.UnSanctionNameToIds.TryGetValue(personId, out var aliases))
            {
                foreach (var alias in aliases)
                {
                    string a = alias.ToUpperInvariant();
                    bool m = fullName.Contains(a) || a.Contains(fullName);
                    s.Lines.Add($"{Bullet}   เทียบกับ \"{alias}\" : {(m ? "ตรง" : "ไม่ตรง")}");
                    if (m) { unHit = true; break; }
                }
            }
            else
            {
                s.Lines.Add($"{Bullet}   ไม่มีชื่อกำกับเลขนี้ในรายชื่อ UN");
            }
        }

        bool hit = thHit || unHit;
        s.Lines.Add(Arrow + (hit
            ? $"เข้าเกณฑ์ — ธุรกรรมเดือนที่รายงานทั้ง {reporting.Count:N0} รายการถูกรายงานเป็น Rule 101"
            : "ไม่เข้าเกณฑ์"));
        return With(s, hit ? ExplainVerdict.Hit : ExplainVerdict.Miss);
    }

    // ------------------------------------------------------------------- 202

    private static ExplainSection Rule202(
        RuleContext ctx, string personId, List<RspTransaction> reporting, List<RspTransaction> historical)
    {
        var s = Section($"Rule 202 — ยอดผิดปกติเทียบค่าเฉลี่ย ({ctx.Config.Rule202PriorMonths} เดือนย้อนหลัง " +
                        $"× {ctx.Config.Rule202Multiplier})", ExplainVerdict.Miss);
        bool anyHit = false;

        foreach (var dir in new[] { "IB", "OB" })
        {
            var dirReporting = reporting.Where(t => t.Direction == dir).ToList();
            s.Lines.Add($"{Bullet}[{dir}]");
            if (dirReporting.Count == 0)
            {
                s.Lines.Add($"{Bullet}   ไม่มีธุรกรรมในเดือนที่รายงาน — ข้าม");
                continue;
            }

            // Same filter as the rule: historical rows never include the reporting month.
            var monthTotals = historical
                .Where(t => t.Direction == dir && t.TransactionDate.HasValue)
                .Where(t => t.TransactionDate!.Value.Year != ctx.ReportingMonth.Year ||
                            t.TransactionDate.Value.Month != ctx.ReportingMonth.Month)
                .GroupBy(t => (year: t.TransactionDate!.Value.Year, month: t.TransactionDate.Value.Month))
                .ToDictionary(g => g.Key, g => g.Sum(t => t.Principal));

            if (monthTotals.Count == 0)
            {
                s.Lines.Add($"{Bullet}   ไม่มีประวัติย้อนหลัง (ลูกค้าใหม่) — ไม่เข้าเกณฑ์ตามข้อกำหนด");
                continue;
            }

            var used = monthTotals
                .OrderByDescending(m => m.Key.year * 100 + m.Key.month)
                .Take(ctx.Config.Rule202PriorMonths)
                .ToList();

            s.Lines.Add($"{Bullet}   ขั้นที่ 1 — เดือนที่มีธุรกรรม (นับเฉพาะเดือนที่ไม่เป็นศูนย์ " +
                        $"เอา {ctx.Config.Rule202PriorMonths} เดือนล่าสุด):");
            foreach (var m in used)
                s.Lines.Add($"{Bullet}      {Ym(m.Key)} = {Money(m.Value)}");

            decimal avg = used.Average(m => m.Value);
            decimal threshold = avg * ctx.Config.Rule202Multiplier;
            decimal total = dirReporting.Sum(t => t.Principal);

            s.Lines.Add($"{Bullet}   ขั้นที่ 2 — ค่าเฉลี่ย = {Money(used.Sum(m => m.Value))} ÷ {used.Count} = {Money(avg)}");
            s.Lines.Add($"{Bullet}   ขั้นที่ 3 — เกณฑ์ = {Money(avg)} × {ctx.Config.Rule202Multiplier} = {Money(threshold)}");
            s.Lines.Add($"{Bullet}   ขั้นที่ 4 — ยอดเดือนที่รายงาน = {Money(total)} " +
                        $"({dirReporting.Count:N0} รายการ)");

            bool breach = total > threshold;
            s.Lines.Add($"{Bullet}   {Money(total)} {(breach ? ">" : "≤")} {Money(threshold)} " +
                        $"→ {(breach ? "เข้าเกณฑ์" : "ไม่เข้าเกณฑ์")}");
            anyHit |= breach;
        }

        s.Lines.Add(Arrow + (anyHit ? "เข้าเกณฑ์ Rule 202" : "ไม่เข้าเกณฑ์ Rule 202"));
        return With(s, anyHit ? ExplainVerdict.Hit : ExplainVerdict.Miss);
    }

    // ------------------------------------------------------------ 203 online

    private static ExplainSection Rule203Online(RuleContext ctx, List<TransactionReportRow> online)
    {
        var s = Section("Rule 203-Online — Transaction Report (ช่องทางออนไลน์)", ExplainVerdict.Miss);
        s.Lines.Add($"{Bullet}ตัวอ่านไฟล์คัดมาแล้วเฉพาะแถวที่ Error Reason ขึ้นต้นด้วย \"Income\" " +
                    "และ Status = Approved/Received/Delivered");
        s.Lines.Add($"{Bullet}แถวที่ Sender ID ตรงกับเลขนี้ : {online.Count:N0} รายการ");
        foreach (var r in online.Take(20))
            s.Lines.Add($"{Bullet}   MTCN {r.MTCN} · {r.Status} · {Money(r.PrincipalAmount ?? 0)} · {r.ErrorReason}");
        if (online.Count > 20) s.Lines.Add($"{Bullet}   … อีก {online.Count - 20:N0} รายการ");

        bool hit = online.Count > 0;
        s.Lines.Add(Arrow + (hit
            ? "เข้าเกณฑ์ — รายงานทั้ง DS_SBE และ DS_SAE (ผ่าน EDD แล้ว จึงตอบ DS_SAE ให้อัตโนมัติ)"
            : "ไม่เข้าเกณฑ์"));
        return With(s, hit ? ExplainVerdict.Hit : ExplainVerdict.Miss);
    }

    // ------------------------------------------------------------ 203 retail

    private static ExplainSection Rule203Retail(
        RuleContext ctx, string personId, List<RspTransaction> reporting, List<RspTransaction> historical)
    {
        var s = Section($"Rule 203-Retail — ยอดไม่สอดคล้องกับรายได้/อาชีพ " +
                        $"(ย้อนหลัง {ctx.Config.Rule203RollingMonths} เดือน × {ctx.Config.Rule203Multiplier})",
                        ExplainVerdict.Miss);

        var excluded = ctx.Config.Rule203ExcludedBranches
            .Select(b => b.Trim().ToUpperInvariant()).ToHashSet();
        s.Lines.Add($"{Bullet}ขั้นที่ 1 — ตัดสาขาออนไลน์ออก: {string.Join(", ", excluded)}");

        var cutoff = ctx.ReportingMonth.AddMonths(-ctx.Config.Rule203RollingMonths);
        var window = reporting.Concat(historical)
            .Where(t => !excluded.Contains(t.BranchAccountId.ToUpperInvariant()))
            .Where(t => t.TransactionDate.HasValue && t.TransactionDate >= cutoff)
            .ToList();
        s.Lines.Add($"{Bullet}ขั้นที่ 2 — ช่วงเวลา: ตั้งแต่ {cutoff:yyyy-MM-dd} เป็นต้นไป " +
                    $"→ {window.Count:N0} รายการ");

        string occ = window.FirstOrDefault(t => !string.IsNullOrWhiteSpace(t.Occupation))?.Occupation ?? "";
        decimal? expected = OccupationReader.FindExpectedIncome(occ, ctx.OccupationMap);
        s.Lines.Add($"{Bullet}ขั้นที่ 3 — อาชีพ \"{(occ.Length > 0 ? occ : "-")}\" → รายได้ที่คาดหมาย " +
                    $"{(expected.HasValue ? Money(expected.Value) + " บาท/เดือน" : "ไม่พบในไฟล์ mapper")}");

        if (expected == null || expected <= 0)
        {
            s.Lines.Add(Arrow + "ไม่เข้าเกณฑ์ — ไม่มีรายได้อ้างอิงให้เทียบ (rule ข้ามบุคคลนี้)");
            return With(s, ExplainVerdict.Skipped);
        }

        decimal threshold = expected.Value * ctx.Config.Rule203Multiplier;
        s.Lines.Add($"{Bullet}ขั้นที่ 4 — เกณฑ์ = {Money(expected.Value)} × {ctx.Config.Rule203Multiplier} " +
                    $"= {Money(threshold)}");

        bool anyHit = false;
        foreach (var dir in new[] { "IB", "OB" })
        {
            decimal windowTotal = window.Where(t => t.Direction == dir).Sum(t => t.Principal);
            int reportingCount = reporting
                .Where(t => !excluded.Contains(t.BranchAccountId.ToUpperInvariant()))
                .Count(t => t.Direction == dir);

            bool over = windowTotal > threshold;
            bool breach = over && reportingCount > 0;
            s.Lines.Add($"{Bullet}   [{dir}] ยอดรวมในช่วง = {Money(windowTotal)} " +
                        $"{(over ? ">" : "≤")} {Money(threshold)}; " +
                        $"ธุรกรรมเดือนที่รายงาน = {reportingCount:N0} รายการ → " +
                        (breach ? "เข้าเกณฑ์"
                                : over ? "เกินเกณฑ์แต่ไม่มีธุรกรรมเดือนที่รายงาน จึงไม่รายงาน"
                                       : "ไม่เข้าเกณฑ์"));
            anyHit |= breach;
        }

        s.Lines.Add(Arrow + (anyHit ? "เข้าเกณฑ์ Rule 203-Retail" : "ไม่เข้าเกณฑ์ Rule 203-Retail"));
        return With(s, anyHit ? ExplainVerdict.Hit : ExplainVerdict.Miss);
    }

    // ------------------------------------------------------------------- 206

    private static ExplainSection Rule206(RuleContext ctx, List<RspTransaction> reporting)
    {
        var s = Section($"Rule 206 — สาขาพื้นที่เสี่ยง (≥ {Money(ctx.Config.Rule206PrincipalThreshold)} บาท " +
                        $"หรือ ≥ {ctx.Config.Rule206TxnCountThreshold} ครั้ง/เดือน)", ExplainVerdict.Miss);

        var branches = (ctx.Config.Rule206HighRiskBranches ?? new List<string>())
            .Select(b => b.Trim().ToUpperInvariant()).ToHashSet();
        if (branches.Count == 0)
        {
            s.Lines.Add($"{Bullet}ยังไม่ได้ตั้งค่าสาขาพื้นที่เสี่ยงในแท็บตั้งค่า");
            s.Lines.Add(Arrow + "ข้าม — rule นี้ไม่ทำงาน");
            return With(s, ExplainVerdict.Skipped);
        }

        s.Lines.Add($"{Bullet}ขั้นที่ 1 — สาขาพื้นที่เสี่ยง: {string.Join(", ", branches)}");
        var inScope = reporting.Where(t => branches.Contains(t.BranchAccountId.ToUpperInvariant())).ToList();
        s.Lines.Add($"{Bullet}ขั้นที่ 2 — ธุรกรรมของบุคคลนี้ที่สาขาดังกล่าว: {inScope.Count:N0} รายการ");

        bool anyHit = false;
        foreach (var dir in new[] { "IB", "OB" })
        {
            var dirTxns = inScope.Where(t => t.Direction == dir).ToList();
            if (dirTxns.Count == 0) continue;
            decimal total = dirTxns.Sum(t => t.Principal);
            bool breach = total >= ctx.Config.Rule206PrincipalThreshold ||
                          dirTxns.Count >= ctx.Config.Rule206TxnCountThreshold;
            s.Lines.Add($"{Bullet}   [{dir}] ยอด {Money(total)} / จำนวน {dirTxns.Count:N0} ครั้ง → " +
                        (breach ? "เข้าเกณฑ์" : "ไม่เข้าเกณฑ์"));
            anyHit |= breach;
        }

        s.Lines.Add(Arrow + (anyHit ? "เข้าเกณฑ์ Rule 206" : "ไม่เข้าเกณฑ์ Rule 206"));
        return With(s, anyHit ? ExplainVerdict.Hit : ExplainVerdict.Miss);
    }

    // ------------------------------------------------------------------- 208

    private static ExplainSection Rule208(RuleContext ctx, List<RspTransaction> reporting)
    {
        var cfg = ctx.Config;
        var s = Section($"Rule 208 — Structuring (ยอดต่ำกว่า {Money(cfg.Rule208SubThreshold)} บาท)",
                        ExplainVerdict.Miss);

        var sub = reporting
            .Where(t => t.Principal < cfg.Rule208SubThreshold && t.Principal > 0 && t.TransactionDate.HasValue)
            .ToList();
        s.Lines.Add($"{Bullet}ขั้นที่ 1 — คัดเฉพาะรายการต่ำกว่าเกณฑ์: {sub.Count:N0} จาก {reporting.Count:N0} รายการ");
        if (sub.Count == 0)
        {
            s.Lines.Add(Arrow + "ไม่เข้าเกณฑ์");
            return With(s, ExplainVerdict.Miss);
        }

        bool anyHit = false;
        foreach (var dir in new[] { "IB", "OB" })
        {
            var txns = sub.Where(t => t.Direction == dir).ToList();
            if (txns.Count == 0) continue;
            s.Lines.Add($"{Bullet}[{dir}]");

            decimal monthTotal = txns.Sum(t => t.Principal);
            bool monthHit = txns.Count > cfg.Rule208MonthlyCount && monthTotal >= cfg.Rule208MonthlyAmount;
            s.Lines.Add($"{Bullet}   รายเดือน : {txns.Count:N0} ครั้ง (เกณฑ์ > {cfg.Rule208MonthlyCount}) " +
                        $"และ {Money(monthTotal)} บาท (เกณฑ์ ≥ {Money(cfg.Rule208MonthlyAmount)}) → " +
                        (monthHit ? "เข้าเกณฑ์" : "ไม่เข้าเกณฑ์"));

            var worstDay = txns.GroupBy(t => t.TransactionDate!.Value.Date)
                .OrderByDescending(g => g.Count()).First();
            bool dayHit = txns.GroupBy(t => t.TransactionDate!.Value.Date).Any(g =>
                g.Count() > cfg.Rule208DailyCount && g.Sum(t => t.Principal) >= cfg.Rule208DailyAmount);
            s.Lines.Add($"{Bullet}   รายวัน (วันที่ถี่สุด {worstDay.Key:yyyy-MM-dd}) : {worstDay.Count():N0} ครั้ง " +
                        $"(เกณฑ์ > {cfg.Rule208DailyCount}) และ {Money(worstDay.Sum(t => t.Principal))} บาท " +
                        $"(เกณฑ์ ≥ {Money(cfg.Rule208DailyAmount)}) → " + (dayHit ? "เข้าเกณฑ์" : "ไม่เข้าเกณฑ์"));

            var byWeek = txns.GroupBy(t => (t.TransactionDate!.Value.Year,
                                            ISOWeek.GetWeekOfYear(t.TransactionDate.Value))).ToList();
            var worstWeek = byWeek.OrderByDescending(g => g.Count()).First();
            bool weekHit = byWeek.Any(g =>
                g.Count() > cfg.Rule208WeeklyCount && g.Sum(t => t.Principal) >= cfg.Rule208WeeklyAmount);
            s.Lines.Add($"{Bullet}   รายสัปดาห์ (สัปดาห์ที่ถี่สุด W{worstWeek.Key.Item2}) : {worstWeek.Count():N0} ครั้ง " +
                        $"(เกณฑ์ > {cfg.Rule208WeeklyCount}) และ {Money(worstWeek.Sum(t => t.Principal))} บาท " +
                        $"(เกณฑ์ ≥ {Money(cfg.Rule208WeeklyAmount)}) → " + (weekHit ? "เข้าเกณฑ์" : "ไม่เข้าเกณฑ์"));

            anyHit |= monthHit || dayHit || weekHit;
        }

        s.Lines.Add(Arrow + (anyHit ? "เข้าเกณฑ์ Rule 208 (เข้าข้อใดข้อหนึ่งก็พอ)" : "ไม่เข้าเกณฑ์ Rule 208"));
        return With(s, anyHit ? ExplainVerdict.Hit : ExplainVerdict.Miss);
    }

    // ------------------------------------------------------------------- 209

    private static ExplainSection Rule209(RuleContext ctx, List<RspTransaction> reporting)
    {
        double hours = ctx.Config.Rule209ConsecutiveHours;
        var s = Section($"Rule 209 — ทำธุรกรรมต่อเนื่อง ≥ {hours} ชั่วโมงในวันเดียว", ExplainVerdict.Miss);

        var withTime = reporting.Where(t => t.TransactionDate.HasValue && t.TransactionTime.HasValue).ToList();
        s.Lines.Add($"{Bullet}ขั้นที่ 1 — รายการที่มีทั้งวันที่และเวลา: {withTime.Count:N0} จาก {reporting.Count:N0}");

        bool anyHit = false;
        foreach (var dir in new[] { "IB", "OB" })
        {
            var days = withTime.Where(t => t.Direction == dir)
                .GroupBy(t => t.TransactionDate!.Value.Date)
                .Where(g => g.Count() >= 2)
                .Select(g =>
                {
                    var times = g.Select(t => t.TransactionTime!.Value).OrderBy(x => x).ToList();
                    return (Date: g.Key, Count: g.Count(), First: times.First(), Last: times.Last(),
                            Span: (times.Last() - times.First()).TotalHours);
                })
                .OrderByDescending(d => d.Span)
                .ToList();

            if (days.Count == 0) continue;
            var top = days[0];
            bool breach = top.Span >= hours;
            s.Lines.Add($"{Bullet}   [{dir}] วันที่ช่วงเวลากว้างสุด {top.Date:yyyy-MM-dd}: " +
                        $"{top.First:hh\\:mm} ถึง {top.Last:hh\\:mm} = {top.Span:F2} ชม. " +
                        $"({top.Count:N0} รายการ) → " + (breach ? "เข้าเกณฑ์" : "ไม่เข้าเกณฑ์"));
            int breachDays = days.Count(d => d.Span >= hours);
            if (breachDays > 1) s.Lines.Add($"{Bullet}      มีอีก {breachDays - 1:N0} วันที่เข้าเกณฑ์เช่นกัน");
            anyHit |= breach;
        }

        if (!anyHit && withTime.Count > 0)
            s.Lines.Add($"{Bullet}   ไม่มีวันใดที่ช่วงเวลาระหว่างรายการแรกกับรายการสุดท้ายถึง {hours} ชม.");

        s.Lines.Add(Arrow + (anyHit ? "เข้าเกณฑ์ Rule 209" : "ไม่เข้าเกณฑ์ Rule 209"));
        return With(s, anyHit ? ExplainVerdict.Hit : ExplainVerdict.Miss);
    }

    // ------------------------------------------------------------------- 212

    private static ExplainSection Rule212(RuleContext ctx, List<RspTransaction> reporting)
    {
        var s = Section("Rule 212 — บุคคลอาชญากรรมทางการเงิน (จับคู่ด้วยชื่อ)", ExplainVerdict.Miss);
        var persons = ctx.CrimeSuspicious.CrimePersons;

        if (persons.Count == 0)
        {
            s.Lines.Add($"{Bullet}ไม่มีรายชื่อในไฟล์ lookup");
            s.Lines.Add(Arrow + "ข้าม");
            return With(s, ExplainVerdict.Skipped);
        }

        var first = reporting.FirstOrDefault();
        if (first == null)
        {
            s.Lines.Add($"{Bullet}ไม่มีธุรกรรมในเดือนที่รายงาน");
            s.Lines.Add(Arrow + "ข้าม");
            return With(s, ExplainVerdict.Skipped);
        }

        string nameKey = first.FullName.ToUpperInvariant();
        string firstKey = first.FirstName.Trim().ToUpperInvariant();
        bool fullHit = persons.Any(p => p.FullNameKey.Equals(nameKey, StringComparison.OrdinalIgnoreCase));
        bool firstOnlyHit = persons.Any(p => string.IsNullOrWhiteSpace(p.LastName) &&
                                             p.FirstName.Trim().Equals(firstKey, StringComparison.OrdinalIgnoreCase));

        s.Lines.Add($"{Bullet}ขั้นที่ 1 — ชื่อใน RSP = \"{nameKey}\" เทียบกับ {persons.Count:N0} รายชื่อ");
        s.Lines.Add($"{Bullet}ขั้นที่ 2 — ตรงทั้งชื่อ-สกุล : {(fullHit ? "พบ" : "ไม่พบ")}");
        s.Lines.Add($"{Bullet}ขั้นที่ 3 — รายการที่มีแต่ชื่อต้น เทียบ \"{firstKey}\" : {(firstOnlyHit ? "พบ" : "ไม่พบ")}");

        bool hit = fullHit || firstOnlyHit;
        s.Lines.Add(Arrow + (hit ? "เข้าเกณฑ์ Rule 212" : "ไม่เข้าเกณฑ์ Rule 212"));
        return With(s, hit ? ExplainVerdict.Hit : ExplainVerdict.Miss);
    }

    // ------------------------------------------------------------------- 301

    private static ExplainSection Rule301(RuleContext ctx, string personId, List<RspTransaction> reporting)
    {
        var s = Section("Rule 301 — พฤติกรรมลูกค้าหน้าร้านน่าสงสัย (MTCN → เลขบัตร → ชื่อ)",
                        ExplainVerdict.Miss);
        var entries = ctx.CrimeSuspicious.SuspiciousRetail;
        if (entries.Count == 0)
        {
            s.Lines.Add($"{Bullet}ไม่มีรายการในไฟล์ lookup");
            s.Lines.Add(Arrow + "ข้าม");
            return With(s, ExplainVerdict.Skipped);
        }

        // The rule picks one key per entry, in priority order.
        var mtcnSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var idSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var nameSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var e in entries)
        {
            if (!string.IsNullOrWhiteSpace(e.MTCN)) mtcnSet.Add(e.MTCN.Trim());
            else if (!string.IsNullOrWhiteSpace(e.IdNumber)) idSet.Add(e.IdNumber.Trim());
            else if (!string.IsNullOrWhiteSpace(e.FullNameKey)) nameSet.Add(e.FullNameKey);
        }
        s.Lines.Add($"{Bullet}ขั้นที่ 1 — รายการ lookup {entries.Count:N0} แถว → " +
                    $"MTCN {mtcnSet.Count:N0}, เลขบัตร {idSet.Count:N0}, ชื่อ {nameSet.Count:N0}");

        var byMtcn = reporting.Where(t => mtcnSet.Contains(t.MTCN.Trim())).ToList();
        bool byId = idSet.Contains(personId);
        var byName = reporting.Where(t => nameSet.Contains(t.FullName.ToUpperInvariant())).ToList();

        s.Lines.Add($"{Bullet}ขั้นที่ 2 — ตรงด้วย MTCN : {byMtcn.Count:N0} รายการ" +
                    (byMtcn.Count > 0 ? $" ({string.Join(", ", byMtcn.Take(5).Select(t => t.MTCN))})" : ""));
        s.Lines.Add($"{Bullet}ขั้นที่ 3 — ตรงด้วยเลขบัตร : {(byId ? "พบ" : "ไม่พบ")}");
        s.Lines.Add($"{Bullet}ขั้นที่ 4 — ตรงด้วยชื่อ-สกุล : {byName.Count:N0} รายการ");

        bool hit = byMtcn.Count > 0 || byId || byName.Count > 0;
        s.Lines.Add(Arrow + (hit ? "เข้าเกณฑ์ Rule 301" : "ไม่เข้าเกณฑ์ Rule 301"));
        return With(s, hit ? ExplainVerdict.Hit : ExplainVerdict.Miss);
    }

    // --------------------------------------------------------------- summary

    private static ExplainSection Summary(List<ExplainSection> sections)
    {
        var s = Section("สรุป", ExplainVerdict.Info);
        var hits = sections.Where(x => x.Verdict == ExplainVerdict.Hit).Select(RuleCodeOf).ToList();

        if (hits.Count == 0)
        {
            s.Lines.Add($"{Bullet}ไม่เข้าเกณฑ์ข้อใดเลย — บุคคลนี้จะไม่ปรากฏใน DS_SBE");
            return s;
        }

        s.Lines.Add($"{Bullet}เข้าเกณฑ์: {string.Join(", ", hits)}");
        string winner = RulePriority.FirstOrDefault(code => hits.Any(h => h.StartsWith(code))) ?? hits[0];
        s.Lines.Add($"{Bullet}DS_SBE เก็บได้ 1 แถวต่อ 1 MTCN — เมื่อ MTCN เดียวเข้าหลายข้อ " +
                    $"จะรายงานตามลำดับ {string.Join(" → ", RulePriority)}");
        s.Lines.Add(Arrow + $"MTCN ที่ซ้ำกันจะถูกรายงานภายใต้ Rule {winner}");
        return s;
    }

    private static string RuleCodeOf(ExplainSection s)
    {
        int i = s.Title.IndexOf("Rule ", StringComparison.Ordinal);
        if (i < 0) return s.Title;
        var rest = s.Title[(i + 5)..];
        int end = rest.IndexOf(' ');
        return end < 0 ? rest : rest[..end];
    }

    // ----------------------------------------------------------------- utils

    private static ExplainSection Section(string title, ExplainVerdict verdict, params string[] lines)
    {
        var s = new ExplainSection { Title = title, Verdict = verdict };
        s.Lines.AddRange(lines);
        return s;
    }

    /// <summary>Copies a section with its final verdict — Verdict is init-only.</summary>
    private static ExplainSection With(ExplainSection s, ExplainVerdict verdict)
    {
        var copy = new ExplainSection { Title = s.Title, Verdict = verdict };
        copy.Lines.AddRange(s.Lines);
        return copy;
    }
}
