using BotReport2026.Models;

namespace BotReport2026.Rules;

public class Rule202AbnormalVolume : IRuleEngine
{
    public string RuleCode => "202";

    public (List<SbeRecord>, List<SaeRecord>) Execute(RuleContext ctx)
    {
        var sbe = new List<SbeRecord>();
        var reportingYM = (ctx.ReportingMonth.Year, ctx.ReportingMonth.Month);
        var branches = IncludedBranches(ctx.Config);

        // Build monthly totals from historical RSP (exclude reporting month as safety filter)
        var historical = ctx.HistoricalRsp
            .Where(t => IsInScope(t, branches))
            .Where(t => t.TransactionDate.HasValue &&
                        (t.TransactionDate.Value.Year != reportingYM.Year ||
                         t.TransactionDate.Value.Month != reportingYM.Month))
            .ToList();

        // Group historical: personId → month → total principal
        var histByPerson = new Dictionary<string, Dictionary<(int year, int month), decimal>>();
        foreach (var t in historical)
        {
            if (string.IsNullOrWhiteSpace(t.PersonId)) continue;
            string key = t.PersonId.Trim().ToUpperInvariant();
            if (!histByPerson.TryGetValue(key, out var monthMap))
            {
                monthMap = new Dictionary<(int, int), decimal>();
                histByPerson[key] = monthMap;
            }
            var ym = (t.TransactionDate!.Value.Year, t.TransactionDate.Value.Month);
            monthMap[ym] = monthMap.GetValueOrDefault(ym) + t.Principal;
        }

        var reportingByPerson = ctx.ReportingMonthRsp
            .Where(t => IsInScope(t, branches))
            .Where(t => !string.IsNullOrWhiteSpace(t.PersonId))
            .GroupBy(t => t.PersonId.Trim(), StringComparer.OrdinalIgnoreCase);

        foreach (var group in reportingByPerson)
        {
            // New customers (no history) are not flagged
            if (!histByPerson.TryGetValue(group.Key.ToUpperInvariant(), out var monthMap) || monthMap.Count == 0)
                continue;

            var activePriorMonths = monthMap
                .OrderByDescending(m => m.Key.year * 100 + m.Key.month)
                .Take(ctx.Config.Rule202PriorMonths)
                .Select(m => m.Value)
                .ToList();

            decimal threshold = activePriorMonths.Average() * ctx.Config.Rule202Multiplier;
            if (group.Sum(t => t.Principal) <= threshold) continue;

            sbe.AddRange(SbeRecordFactory.From(group, RuleCode, ctx));
        }

        ctx.LogCallback($"Rule 202: พบ {sbe.Count} รายการยอดผิดปกติ (avg × {ctx.Config.Rule202Multiplier}, " +
                        $"เฉพาะขาออก สาขา {BranchScope.Describe(branches)})");
        return (sbe, new List<SaeRecord>());
    }

    /// <summary>Rule202Branches — outbound at these branches only. Empty = every branch.</summary>
    public static HashSet<string> IncludedBranches(AppConfig config) =>
        BranchScope.Normalize(config.Rule202Branches);

    /// <summary>
    /// Rule 202 looks only at outbound (send money) transactions made at the configured
    /// branches (the online channel by default) — both for the history and the reporting month.
    /// </summary>
    public static bool IsInScope(RspTransaction t, HashSet<string> branches) =>
        BranchScope.IsOutboundAt(t, branches);
}
