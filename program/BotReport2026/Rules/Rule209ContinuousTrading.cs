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

        foreach (var personGroup in byPerson)
        {
            var byDay = personGroup.GroupBy(t => t.TransactionDate!.Value.Date);
            bool personFlagged = false;
            List<RspTransaction> flaggedTxns = new();

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
                {
                    personFlagged = true;
                    flaggedTxns.AddRange(dayTxns);
                }
            }

            if (!personFlagged || flaggedTxns.Count == 0) continue;

            var first = flaggedTxns[0];
            sbe.Add(new SbeRecord
            {
                ReportingPeriod = period,
                FirstName = first.FirstName,
                LastName = first.LastName,
                PersonRefId = personGroup.Key,
                AnomalyDate = flaggedTxns.Min(t => t.TransactionDate),
                BehaviorType = "209",
                RuleCode = RuleCode,
                MtcnList = string.Join(", ", flaggedTxns.Select(t => t.MTCN).Distinct()),
                TransactionCount = flaggedTxns.Count,
                TotalAmount = flaggedTxns.Sum(t => t.Principal)
            });
        }

        ctx.LogCallback($"Rule 209: พบ {sbe.Count} รายการทำธุรกรรมต่อเนื่อง ≥{hours} ชั่วโมง");
        return (sbe, new List<SaeRecord>());
    }
}
