using BotReport2026.Models;

namespace BotReport2026.Rules;

public class Rule202AbnormalVolume : IRuleEngine
{
    public string RuleCode => "202";

    public (List<SbeRecord>, List<SaeRecord>) Execute(RuleContext ctx)
    {
        var sbe = new List<SbeRecord>();
        var period = ctx.ReportingMonth.ToString("yyyy-MM");
        var reportingYM = (ctx.ReportingMonth.Year, ctx.ReportingMonth.Month);

        // Build monthly totals from historical RSP (exclude reporting month as safety filter)
        var historical = ctx.HistoricalRsp
            .Where(t => t.TransactionDate.HasValue &&
                        (t.TransactionDate.Value.Year != reportingYM.Year ||
                         t.TransactionDate.Value.Month != reportingYM.Month))
            .ToList();

        // Group historical: personId → month → total principal
        var histByPerson = new Dictionary<string, Dictionary<(int year, int month), decimal>>(StringComparer.OrdinalIgnoreCase);
        foreach (var t in historical)
        {
            if (string.IsNullOrWhiteSpace(t.PersonId)) continue;
            if (!histByPerson.TryGetValue(t.PersonId, out var monthMap))
            {
                monthMap = new Dictionary<(int, int), decimal>();
                histByPerson[t.PersonId] = monthMap;
            }
            var ym = (t.TransactionDate!.Value.Year, t.TransactionDate.Value.Month);
            monthMap[ym] = monthMap.GetValueOrDefault(ym) + t.Principal;
        }

        // Reporting month totals
        var reportingByPerson = ctx.ReportingMonthRsp
            .Where(t => !string.IsNullOrWhiteSpace(t.PersonId))
            .GroupBy(t => t.PersonId.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        foreach (var kvp in reportingByPerson)
        {
            string personId = kvp.Key;
            var txns = kvp.Value;
            decimal reportingTotal = txns.Sum(t => t.Principal);

            // Skip if person has no history (new customer)
            if (!histByPerson.TryGetValue(personId, out var monthMap) || monthMap.Count == 0)
                continue;

            // Take only the N most recent prior months (months with activity)
            var activePriorMonths = monthMap
                .OrderByDescending(m => m.Key.year * 100 + m.Key.month)
                .Take(ctx.Config.Rule202PriorMonths)
                .Select(m => m.Value)
                .ToList();

            if (activePriorMonths.Count == 0) continue;

            decimal avg = activePriorMonths.Average();
            decimal threshold = avg * ctx.Config.Rule202Multiplier;

            if (reportingTotal > threshold)
            {
                var first = txns[0];
                sbe.Add(new SbeRecord
                {
                    ReportingPeriod = period,
                    FirstName = first.FirstName,
                    LastName = first.LastName,
                    PersonRefId = personId,
                    AnomalyDate = txns.Min(t => t.TransactionDate),
                    BehaviorType = "202",
                    RuleCode = RuleCode,
                    MtcnList = string.Join(", ", txns.Select(t => t.MTCN).Distinct()),
                    TransactionCount = txns.Count,
                    TotalAmount = reportingTotal,
                });
            }
        }

        ctx.LogCallback($"Rule 202: พบ {sbe.Count} รายการยอดผิดปกติ (avg × {ctx.Config.Rule202Multiplier})");
        return (sbe, new List<SaeRecord>());
    }
}
