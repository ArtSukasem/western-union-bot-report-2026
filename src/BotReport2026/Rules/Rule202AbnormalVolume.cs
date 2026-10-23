using BotReport2026.Models;

namespace BotReport2026.Rules;

public class Rule202AbnormalVolume : IRuleEngine
{
    public string RuleCode => "202";

    public (List<SbeRecord>, List<SaeRecord>) Execute(RuleContext ctx)
    {
        var sbe = new List<SbeRecord>();
        var reportingYM = (ctx.ReportingMonth.Year, ctx.ReportingMonth.Month);

        // Build monthly totals from historical RSP (exclude reporting month as safety filter)
        var historical = ctx.HistoricalRsp
            .Where(t => t.TransactionDate.HasValue &&
                        (t.TransactionDate.Value.Year != reportingYM.Year ||
                         t.TransactionDate.Value.Month != reportingYM.Month))
            .ToList();

        // Group historical: (personId, direction) → month → total principal
        var histByPerson = new Dictionary<(string personId, string direction), Dictionary<(int year, int month), decimal>>();
        foreach (var t in historical)
        {
            if (string.IsNullOrWhiteSpace(t.PersonId)) continue;
            var key = (t.PersonId.Trim().ToUpperInvariant(), t.Direction);
            if (!histByPerson.TryGetValue(key, out var monthMap))
            {
                monthMap = new Dictionary<(int, int), decimal>();
                histByPerson[key] = monthMap;
            }
            var ym = (t.TransactionDate!.Value.Year, t.TransactionDate.Value.Month);
            monthMap[ym] = monthMap.GetValueOrDefault(ym) + t.Principal;
        }

        // Reporting month totals, grouped by person only (direction split happens below)
        var reportingByPerson = ctx.ReportingMonthRsp
            .Where(t => !string.IsNullOrWhiteSpace(t.PersonId))
            .GroupBy(t => t.PersonId.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        // Evaluates one direction's breach: true if it has history and reportingTotal > avg * multiplier
        bool IsBreach(string personId, string direction, List<RspTransaction> txns)
        {
            if (txns.Count == 0) return false;
            if (!histByPerson.TryGetValue((personId.ToUpperInvariant(), direction), out var monthMap) || monthMap.Count == 0)
                return false;

            var activePriorMonths = monthMap
                .OrderByDescending(m => m.Key.year * 100 + m.Key.month)
                .Take(ctx.Config.Rule202PriorMonths)
                .Select(m => m.Value)
                .ToList();

            if (activePriorMonths.Count == 0) return false;

            decimal avg = activePriorMonths.Average();
            decimal threshold = avg * ctx.Config.Rule202Multiplier;
            return txns.Sum(t => t.Principal) > threshold;
        }

        foreach (var kvp in reportingByPerson)
        {
            string personId = kvp.Key;
            var ibTxns = kvp.Value.Where(t => t.Direction == "IB").ToList();
            var obTxns = kvp.Value.Where(t => t.Direction == "OB").ToList();

            bool ibBreach = IsBreach(personId, "IB", ibTxns);
            bool obBreach = IsBreach(personId, "OB", obTxns);

            var resolved = DirectionalFlagResolver.Resolve(ibBreach, ibTxns, obBreach, obTxns);
            if (resolved == null) continue;
            var (txns, suffix) = resolved.Value;

            sbe.AddRange(SbeRecordFactory.From(txns, RuleCode + suffix, ctx));
        }

        ctx.LogCallback($"Rule 202: พบ {sbe.Count} รายการยอดผิดปกติ (avg × {ctx.Config.Rule202Multiplier})");
        return (sbe, new List<SaeRecord>());
    }
}
