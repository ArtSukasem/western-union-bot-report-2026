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
            var ibTxns = group.Where(t => t.Direction == "IB").ToList();
            var obTxns = group.Where(t => t.Direction == "OB").ToList();

            bool ibBreach = ibTxns.Count > 0 &&
                (ibTxns.Sum(t => t.Principal) >= ctx.Config.Rule206PrincipalThreshold ||
                 ibTxns.Count >= ctx.Config.Rule206TxnCountThreshold);
            bool obBreach = obTxns.Count > 0 &&
                (obTxns.Sum(t => t.Principal) >= ctx.Config.Rule206PrincipalThreshold ||
                 obTxns.Count >= ctx.Config.Rule206TxnCountThreshold);

            var resolved = DirectionalFlagResolver.Resolve(ibBreach, ibTxns, obBreach, obTxns);
            if (resolved == null) continue;
            var (txns, suffix) = resolved.Value;

            var first = txns[0];
            sbe.AddRange(SbeRecordFactory.From(txns, RuleCode + suffix, ctx));
        }

        ctx.LogCallback($"Rule 206: พบ {sbe.Count} รายการจากสาขาพื้นที่เสี่ยง");
        return (sbe, new List<SaeRecord>());
    }
}
