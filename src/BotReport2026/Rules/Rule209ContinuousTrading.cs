using BotReport2026.Models;

namespace BotReport2026.Rules;

public class Rule209ContinuousTrading : IRuleEngine
{
    public string RuleCode => "209";

    public (List<SbeRecord>, List<SaeRecord>) Execute(RuleContext ctx)
    {
        var sbe = new List<SbeRecord>();
        double hours = ctx.Config.Rule209ConsecutiveHours;
        var branches = IncludedBranches(ctx.Config);

        // Outbound at the configured branches only
        var withTime = ctx.ReportingMonthRsp
            .Where(t => BranchScope.IsOutboundAt(t, branches))
            .Where(t => t.TransactionDate.HasValue && t.TransactionTime.HasValue)
            .Where(t => !string.IsNullOrWhiteSpace(t.PersonId))
            .ToList();

        // Group by personId, then by date
        var byPerson = withTime.GroupBy(t => t.PersonId.Trim(), StringComparer.OrdinalIgnoreCase);

        List<RspTransaction> CollectFlagged(IEnumerable<RspTransaction> personTxns)
        {
            var flagged = new List<RspTransaction>();
            var byDay = personTxns.GroupBy(t => t.TransactionDate!.Value.Date);
            foreach (var dayGroup in byDay)
            {
                var dayTxns = dayGroup.ToList();
                var times = dayTxns
                    .Select(t => t.TransactionTime!.Value)
                    .OrderBy(ts => ts)
                    .ToList();

                if (times.Count < 2) continue;

                double spanHours = (times.Last() - times.First()).TotalHours;
                if (spanHours >= hours)
                    flagged.AddRange(dayTxns);
            }
            return flagged;
        }

        foreach (var personGroup in byPerson)
        {
            var flaggedTxns = CollectFlagged(personGroup);
            if (flaggedTxns.Count == 0) continue;

            sbe.AddRange(SbeRecordFactory.From(flaggedTxns, RuleCode, ctx));
        }

        ctx.LogCallback($"Rule 209: พบ {sbe.Count} รายการทำธุรกรรมต่อเนื่อง ≥{hours} ชั่วโมง " +
                        $"(เฉพาะขาออก สาขา {BranchScope.Describe(branches)})");
        return (sbe, new List<SaeRecord>());
    }

    /// <summary>Rule209Branches — outbound at these branches only. Empty = every branch.</summary>
    public static HashSet<string> IncludedBranches(AppConfig config) =>
        BranchScope.Normalize(config.Rule209Branches);
}
