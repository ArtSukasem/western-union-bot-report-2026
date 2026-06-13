using BotReport2026.Models;

namespace BotReport2026.Rules;

/// <summary>
/// Rule 301 — พฤติกรรมลูกค้าหน้าร้านน่าสงสัย.
/// For each entry, match RSP transactions by priority: MTCN → IDnumber → ชื่อ-สกุล.
/// Flag matched transactions → DS_SBE.
/// </summary>
public class Rule301SuspiciousRetail : IRuleEngine
{
    public string RuleCode => "301";

    public (List<SbeRecord>, List<SaeRecord>) Execute(RuleContext ctx)
    {
        var sbe = new List<SbeRecord>();
        var period = ctx.ReportingMonth.ToString("yyyy-MM");

        var entries = ctx.CrimeSuspicious.SuspiciousRetail;
        if (entries.Count == 0)
        {
            ctx.LogCallback("Rule 301: ไม่มีรายการพฤติกรรมหน้าร้านน่าสงสัย — ข้าม");
            return (sbe, new List<SaeRecord>());
        }

        // Per-entry active matching key (MTCN → ID → name)
        var mtcnSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var idSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var nameSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var e in entries)
        {
            if (!string.IsNullOrWhiteSpace(e.MTCN)) mtcnSet.Add(e.MTCN.Trim());
            else if (!string.IsNullOrWhiteSpace(e.IdNumber)) idSet.Add(e.IdNumber.Trim());
            else if (!string.IsNullOrWhiteSpace(e.FullNameKey)) nameSet.Add(e.FullNameKey);
        }

        // Collect matched transactions (dedupe by MTCN+Direction)
        var matched = new Dictionary<string, RspTransaction>();
        foreach (var t in ctx.ReportingMonthRsp)
        {
            bool hit =
                (mtcnSet.Count > 0 && mtcnSet.Contains(t.MTCN.Trim())) ||
                (idSet.Count > 0 && !string.IsNullOrWhiteSpace(t.PersonId) && idSet.Contains(t.PersonId.Trim())) ||
                (nameSet.Count > 0 && nameSet.Contains(t.FullName.ToUpperInvariant()));
            if (hit)
                matched[$"{t.MTCN}|{t.Direction}"] = t;
        }

        // Group matched transactions by person (fallback to MTCN if no person id)
        var byPerson = matched.Values.GroupBy(t =>
            !string.IsNullOrWhiteSpace(t.PersonId) ? t.PersonId.Trim() : $"MTCN:{t.MTCN}",
            StringComparer.OrdinalIgnoreCase);

        foreach (var group in byPerson)
        {
            var txns = group.ToList();
            var first = txns[0];
            sbe.Add(new SbeRecord
            {
                ReportingPeriod = period,
                FirstName = first.FirstName,
                LastName = first.LastName,
                PersonRefId = first.PersonId,
                AnomalyDate = txns.Min(t => t.TransactionDate),
                BehaviorType = "301",
                RuleCode = RuleCode,
                MtcnList = string.Join(", ", txns.Select(t => t.MTCN).Distinct()),
                TransactionCount = txns.Count,
                TotalAmount = txns.Sum(t => t.Principal)
            });
        }

        ctx.LogCallback($"Rule 301: พบ {sbe.Count} รายการตรงกับพฤติกรรมหน้าร้านน่าสงสัย");
        return (sbe, new List<SaeRecord>());
    }
}
