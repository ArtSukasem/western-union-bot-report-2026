using BotReport2026.Models;

namespace BotReport2026.Rules;

public class Rule206HighRiskBranch : IRuleEngine
{
    public string RuleCode => "206";

    public (List<SbeRecord>, List<SaeRecord>) Execute(RuleContext ctx)
    {
        var sbe = new List<SbeRecord>();
        var period = ctx.ReportingMonth.ToString("yyyy-MM");

        if (ctx.Config.Rule206HighRiskBranches == null || ctx.Config.Rule206HighRiskBranches.Count == 0)
        {
            ctx.LogCallback("Rule 206: ไม่มีสาขาพื้นที่เสี่ยง — ข้าม");
            return (sbe, new List<SaeRecord>());
        }

        var highRiskBranches = ctx.Config.Rule206HighRiskBranches
            .Select(b => b.Trim().ToUpperInvariant())
            .ToHashSet();

        var filtered = ctx.ReportingMonthRsp
            .Where(t => highRiskBranches.Contains(t.BranchAccountId.ToUpperInvariant()))
            .Where(t => !string.IsNullOrWhiteSpace(t.PersonId))
            .GroupBy(t => t.PersonId.Trim(), StringComparer.OrdinalIgnoreCase);

        foreach (var group in filtered)
        {
            var txns = group.ToList();
            decimal total = txns.Sum(t => t.Principal);
            int count = txns.Count;

            if (total >= ctx.Config.Rule206PrincipalThreshold ||
                count >= ctx.Config.Rule206TxnCountThreshold)
            {
                var first = txns[0];
                sbe.Add(new SbeRecord
                {
                    ReportingPeriod = period,
                    FirstName = first.FirstName,
                    LastName = first.LastName,
                    PersonRefId = group.Key,
                    AnomalyDate = txns.Min(t => t.TransactionDate),
                    BehaviorType = "206",
                    RuleCode = RuleCode,
                    MtcnList = string.Join(", ", txns.Select(t => t.MTCN).Distinct()),
                    TransactionCount = count,
                    TotalAmount = total
                });
            }
        }

        ctx.LogCallback($"Rule 206: พบ {sbe.Count} รายการจากสาขาพื้นที่เสี่ยง");
        return (sbe, new List<SaeRecord>());
    }
}
