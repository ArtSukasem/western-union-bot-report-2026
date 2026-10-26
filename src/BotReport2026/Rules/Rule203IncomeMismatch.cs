using BotReport2026.Models;
using BotReport2026.Readers;

namespace BotReport2026.Rules;

public class Rule203IncomeMismatch : IRuleEngine
{
    public string RuleCode => "203";

    public (List<SbeRecord>, List<SaeRecord>) Execute(RuleContext ctx)
    {
        var sbeList = new List<SbeRecord>();

        // --- 203 Online: Transaction Report rows (already pre-filtered by reader) ---
        // These have already been through EDD — CSC called the customer and the
        // transaction was subsequently approved — so the DS_SAE row is pre-answered
        // downstream by SaeRecordFactory rather than left for staff.
        foreach (var row in ctx.TransactionReport)
            sbeList.Add(SbeRecordFactory.FromOnline(row, "203-Online", ctx));

        // --- 203 Retail: outbound (send money) RSP only, excluding online branches ---
        var excludedBranches = ExcludedBranches(ctx.Config);

        // Combine reporting + historical, keep retail outbound
        var allRetail = ctx.ReportingMonthRsp.Concat(ctx.HistoricalRsp)
            .Where(t => IsRetailOutbound(t, excludedBranches))
            .Where(t => t.TransactionDate.HasValue)
            .ToList();

        // Determine rolling window cutoff: reporting month + prior N months
        var cutoff = ctx.ReportingMonth.AddMonths(-ctx.Config.Rule203RollingMonths);

        var inWindow = allRetail
            .Where(t => t.TransactionDate >= cutoff)
            .ToList();

        // Group by personId
        var byPerson = inWindow
            .Where(t => !string.IsNullOrWhiteSpace(t.PersonId))
            .GroupBy(t => t.PersonId.Trim(), StringComparer.OrdinalIgnoreCase);

        int otherIncomeCount = 0;
        foreach (var group in byPerson)
        {
            string personId = group.Key;
            var (match, _) = ResolveIncome(group, ctx.Occupations);
            decimal expectedIncome;
            if (match != null)
                expectedIncome = match.Entry.ExpectedIncome!.Value;
            else
            {
                // Occupation is "อื่น ๆ" — nothing in the lookup gives an income
                expectedIncome = ctx.Config.Rule203OtherMonthlyIncome;
                otherIncomeCount++;
            }
            if (expectedIncome <= 0) continue;

            decimal threshold = expectedIncome * ctx.Config.Rule203Multiplier;
            decimal windowTotal = group.Sum(t => t.Principal);

            // Flag only this person's reporting-month transactions; a breach with no
            // reporting-month activity has nothing to report
            var reportingTxns = ctx.ReportingMonthRsp
                .Where(t => IsRetailOutbound(t, excludedBranches))
                .Where(t => t.PersonId.Equals(personId, StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (windowTotal <= threshold || reportingTxns.Count == 0) continue;

            sbeList.AddRange(SbeRecordFactory.From(reportingTxns, "203-Retail", ctx));
        }

        if (otherIncomeCount > 0)
            ctx.LogCallback($"Rule 203 Retail: {otherIncomeCount:N0} คนไม่พบรายได้ตามอาชีพในไฟล์ lookup " +
                            $"→ ใช้รายได้อาชีพอื่น ๆ {ctx.Config.Rule203OtherMonthlyIncome:N0} บาท/เดือน");
        ctx.LogCallback($"Rule 203: Online={ctx.TransactionReport.Count}, Retail={sbeList.Count - ctx.TransactionReport.Count} รายการ");
        return (sbeList, new List<SaeRecord>());
    }

    /// <summary>Rule203ExcludedBranches, trimmed and uppercased for lookup.</summary>
    public static HashSet<string> ExcludedBranches(AppConfig config) =>
        config.Rule203ExcludedBranches
            .Select(b => b.Trim().ToUpperInvariant())
            .ToHashSet();

    /// <summary>
    /// Rule 203-Retail looks only at outbound (send money) transactions, and only those
    /// made outside the excluded (online) branches.
    /// </summary>
    public static bool IsRetailOutbound(RspTransaction t, HashSet<string> excludedBranches) =>
        t.Direction == "OB" &&
        !excludedBranches.Contains(t.BranchAccountId.Trim().ToUpperInvariant());

    /// <summary>
    /// The person's expected monthly income: the first transaction (reporting month first,
    /// then history) whose occupation resolves to a lookup row with an income.
    /// <c>match</c> is null when none does; <c>source</c> is then the first transaction
    /// that had any occupation text at all, for display — or null if none had any.
    /// </summary>
    public static (OccupationMatch? match, RspTransaction? source) ResolveIncome(
        IEnumerable<RspTransaction> txns, OccupationLookup lookup)
    {
        RspTransaction? firstWithText = null;
        foreach (var t in txns)
        {
            if (firstWithText == null &&
                (!string.IsNullOrWhiteSpace(t.Occupation) || !string.IsNullOrWhiteSpace(t.OccupationOther)))
                firstWithText = t;

            var m = lookup.Resolve(t.Occupation, t.OccupationOther);
            if (m?.Entry.ExpectedIncome is > 0) return (m, t);
        }
        return (null, firstWithText);
    }

    /// <summary>"OTHERS / HOUSEWIFT" — the dropdown value plus the free text when there is one.</summary>
    public static string OccupationText(RspTransaction t) =>
        string.IsNullOrWhiteSpace(t.OccupationOther)
            ? t.Occupation
            : $"{t.Occupation} / {t.OccupationOther}";
}
