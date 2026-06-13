using BotReport2026.Models;
using System.Globalization;

namespace BotReport2026.Rules;

public class Rule208Structuring : IRuleEngine
{
    public string RuleCode => "208";

    public (List<SbeRecord>, List<SaeRecord>) Execute(RuleContext ctx)
    {
        var sbe = new List<SbeRecord>();
        var period = ctx.ReportingMonth.ToString("yyyy-MM");
        var cfg = ctx.Config;

        // Filter: only sub-threshold transactions
        var subTxns = ctx.ReportingMonthRsp
            .Where(t => t.Principal < cfg.Rule208SubThreshold && t.Principal > 0)
            .Where(t => !string.IsNullOrWhiteSpace(t.PersonId))
            .ToList();

        var byPerson = subTxns.GroupBy(t => t.PersonId.Trim(), StringComparer.OrdinalIgnoreCase);

        foreach (var group in byPerson)
        {
            var txns = group.Where(t => t.TransactionDate.HasValue).ToList();
            if (txns.Count == 0) continue;

            bool flagged = false;

            // Monthly check
            decimal monthlyTotal = txns.Sum(t => t.Principal);
            if (txns.Count > cfg.Rule208MonthlyCount && monthlyTotal >= cfg.Rule208MonthlyAmount)
                flagged = true;

            if (!flagged)
            {
                // Daily check
                var byDay = txns.GroupBy(t => t.TransactionDate!.Value.Date);
                foreach (var dayGroup in byDay)
                {
                    var dayTxns = dayGroup.ToList();
                    if (dayTxns.Count > cfg.Rule208DailyCount &&
                        dayTxns.Sum(t => t.Principal) >= cfg.Rule208DailyAmount)
                    {
                        flagged = true;
                        break;
                    }
                }
            }

            if (!flagged)
            {
                // Weekly check (ISO week)
                var byWeek = txns.GroupBy(t =>
                    (t.TransactionDate!.Value.Year,
                     ISOWeek.GetWeekOfYear(t.TransactionDate.Value)));
                foreach (var weekGroup in byWeek)
                {
                    var wkTxns = weekGroup.ToList();
                    if (wkTxns.Count > cfg.Rule208WeeklyCount &&
                        wkTxns.Sum(t => t.Principal) >= cfg.Rule208WeeklyAmount)
                    {
                        flagged = true;
                        break;
                    }
                }
            }

            if (!flagged) continue;

            var first = txns[0];
            sbe.Add(new SbeRecord
            {
                ReportingPeriod = period,
                FirstName = first.FirstName,
                LastName = first.LastName,
                PersonRefId = group.Key,
                AnomalyDate = txns.Min(t => t.TransactionDate),
                BehaviorType = "208",
                RuleCode = RuleCode,
                MtcnList = string.Join(", ", txns.Select(t => t.MTCN).Distinct()),
                TransactionCount = txns.Count,
                TotalAmount = txns.Sum(t => t.Principal)
            });
        }

        ctx.LogCallback($"Rule 208: พบ {sbe.Count} รายการ Structuring");
        return (sbe, new List<SaeRecord>());
    }
}
