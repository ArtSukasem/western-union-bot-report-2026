using BotReport2026.Models;

namespace BotReport2026.Rules;

public class Rule209ContinuousTrading : IRuleEngine
{
    public string RuleCode => "209";

    public (List<SbeRecord>, List<SaeRecord>) Execute(RuleContext ctx)
    {
        var sbe = new List<SbeRecord>();
        var period = ctx.ReportingMonth.ToString("yyyy-MM");
        double hours = ctx.Config.Rule209ConsecutiveHours;

        var withTime = ctx.ReportingMonthRsp
            .Where(t => t.TransactionDate.HasValue && t.TransactionTime.HasValue)
            .Where(t => !string.IsNullOrWhiteSpace(t.PersonId))
            .ToList();

        // Group by personId, then by date
        var byPerson = withTime.GroupBy(t => t.PersonId.Trim(), StringComparer.OrdinalIgnoreCase);

        List<RspTransaction> CollectFlagged(IEnumerable<RspTransaction> directionTxns)
        {
            var flagged = new List<RspTransaction>();
            var byDay = directionTxns.GroupBy(t => t.TransactionDate!.Value.Date);
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
            var ibTxns = personGroup.Where(t => t.Direction == "IB").ToList();
            var obTxns = personGroup.Where(t => t.Direction == "OB").ToList();

            var ibFlagged = CollectFlagged(ibTxns);
            var obFlagged = CollectFlagged(obTxns);

            var resolved = DirectionalFlagResolver.Resolve(
                ibFlagged.Count > 0, ibFlagged,
                obFlagged.Count > 0, obFlagged);
            if (resolved == null) continue;
            var (flaggedTxns, suffix) = resolved.Value;

            var first = flaggedTxns[0];
            sbe.AddRange(SbeRecordFactory.From(flaggedTxns, RuleCode + suffix, ctx));
        }

        ctx.LogCallback($"Rule 209: พบ {sbe.Count} รายการทำธุรกรรมต่อเนื่อง ≥{hours} ชั่วโมง");
        return (sbe, new List<SaeRecord>());
    }
}
