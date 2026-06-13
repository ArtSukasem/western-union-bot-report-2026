using BotReport2026.Models;

namespace BotReport2026.Rules;

/// <summary>
/// Rule 212 — บุคคลอาชญากรรมทางการเงิน.
/// Match RSP person name (IB receiver / OB sender) against the financial-crime
/// persons list (Firstname + Lastname). Flag all that person's transactions → DS_SBE.
/// </summary>
public class Rule212FinancialCrime : IRuleEngine
{
    public string RuleCode => "212";

    public (List<SbeRecord>, List<SaeRecord>) Execute(RuleContext ctx)
    {
        var sbe = new List<SbeRecord>();
        var period = ctx.ReportingMonth.ToString("yyyy-MM");

        // Build lookup of crime names (full name + first-only fallback)
        var crimeFullNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var crimeFirstNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var c in ctx.CrimeSuspicious.CrimePersons)
        {
            if (!string.IsNullOrWhiteSpace(c.FullNameKey))
                crimeFullNames.Add(c.FullNameKey);
            if (string.IsNullOrWhiteSpace(c.LastName) && !string.IsNullOrWhiteSpace(c.FirstName))
                crimeFirstNames.Add(c.FirstName.Trim().ToUpperInvariant());
        }

        if (crimeFullNames.Count == 0 && crimeFirstNames.Count == 0)
        {
            ctx.LogCallback("Rule 212: ไม่มีรายชื่อบุคคลอาชญากรรม — ข้าม");
            return (sbe, new List<SaeRecord>());
        }

        var byPerson = ctx.ReportingMonthRsp
            .Where(t => !string.IsNullOrWhiteSpace(t.FullName))
            .GroupBy(t => t.PersonId.Trim(), StringComparer.OrdinalIgnoreCase);

        foreach (var group in byPerson)
        {
            var txns = group.ToList();
            string nameKey = txns[0].FullName.ToUpperInvariant();
            string firstKey = txns[0].FirstName.Trim().ToUpperInvariant();

            bool hit = crimeFullNames.Contains(nameKey) ||
                       (crimeFirstNames.Count > 0 && crimeFirstNames.Contains(firstKey));
            if (!hit) continue;

            var first = txns[0];
            sbe.Add(new SbeRecord
            {
                ReportingPeriod = period,
                FirstName = first.FirstName,
                LastName = first.LastName,
                PersonRefId = group.Key,
                AnomalyDate = txns.Min(t => t.TransactionDate),
                BehaviorType = "212",
                RuleCode = RuleCode,
                MtcnList = string.Join(", ", txns.Select(t => t.MTCN).Distinct()),
                TransactionCount = txns.Count,
                TotalAmount = txns.Sum(t => t.Principal)
            });
        }

        ctx.LogCallback($"Rule 212: พบ {sbe.Count} รายการตรงกับบุคคลอาชญากรรมทางการเงิน");
        return (sbe, new List<SaeRecord>());
    }
}
