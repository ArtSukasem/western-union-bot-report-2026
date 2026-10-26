using BotReport2026.Models;
using System.Globalization;

namespace BotReport2026.Rules;

public class Rule208Structuring : IRuleEngine
{
    public string RuleCode => "208";

    public (List<SbeRecord>, List<SaeRecord>) Execute(RuleContext ctx)
    {
        var sbe = new List<SbeRecord>();
        var cfg = ctx.Config;
        var branches = IncludedBranches(cfg);

        // Filter: outbound at the configured branches, sub-threshold transactions only
        var subTxns = ctx.ReportingMonthRsp
            .Where(t => BranchScope.IsOutboundAt(t, branches))
            .Where(t => t.Principal < cfg.Rule208SubThreshold && t.Principal > 0)
            .Where(t => !string.IsNullOrWhiteSpace(t.PersonId))
            .ToList();

        var byPerson = subTxns.GroupBy(t => t.PersonId.Trim(), StringComparer.OrdinalIgnoreCase);

        bool IsFlagged(List<RspTransaction> txns)
        {
            if (txns.Count == 0) return false;

            // Monthly check
            decimal monthlyTotal = txns.Sum(t => t.Principal);
            if (txns.Count > cfg.Rule208MonthlyCount && monthlyTotal >= cfg.Rule208MonthlyAmount)
                return true;

            // Daily check
            var byDay = txns.GroupBy(t => t.TransactionDate!.Value.Date);
            foreach (var dayGroup in byDay)
            {
                var dayTxns = dayGroup.ToList();
                if (dayTxns.Count > cfg.Rule208DailyCount &&
                    dayTxns.Sum(t => t.Principal) >= cfg.Rule208DailyAmount)
                    return true;
            }

            // Weekly check (ISO week)
            var byWeek = txns.GroupBy(t =>
                (t.TransactionDate!.Value.Year,
                 ISOWeek.GetWeekOfYear(t.TransactionDate.Value)));
            foreach (var weekGroup in byWeek)
            {
                var wkTxns = weekGroup.ToList();
                if (wkTxns.Count > cfg.Rule208WeeklyCount &&
                    wkTxns.Sum(t => t.Principal) >= cfg.Rule208WeeklyAmount)
                    return true;
            }

            return false;
        }

        foreach (var group in byPerson)
        {
            var txns = group.Where(t => t.TransactionDate.HasValue).ToList();
            if (!IsFlagged(txns)) continue;

            sbe.AddRange(SbeRecordFactory.From(txns, RuleCode, ctx));
        }

        ctx.LogCallback($"Rule 208: พบ {sbe.Count} รายการ Structuring (เฉพาะขาออก สาขา {BranchScope.Describe(branches)})");
        return (sbe, new List<SaeRecord>());
    }

    /// <summary>Rule208Branches — outbound at these branches only. Empty = every branch.</summary>
    public static HashSet<string> IncludedBranches(AppConfig config) =>
        BranchScope.Normalize(config.Rule208Branches);
}
