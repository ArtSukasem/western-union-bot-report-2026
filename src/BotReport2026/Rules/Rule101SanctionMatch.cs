using BotReport2026.Models;

namespace BotReport2026.Rules;

public class Rule101SanctionMatch : IRuleEngine
{
    public string RuleCode => "101";

    public (List<SbeRecord>, List<SaeRecord>) Execute(RuleContext ctx)
    {
        var sbe = new List<SbeRecord>();

        // Group by PersonId
        var byPerson = ctx.ReportingMonthRsp
            .Where(t => !string.IsNullOrWhiteSpace(t.PersonId))
            .GroupBy(t => t.PersonId.Trim(), StringComparer.OrdinalIgnoreCase);

        foreach (var group in byPerson)
        {
            string personId = group.Key;
            bool hit = false;

            // TH/CFR match — ID only
            if (ctx.SanctionData.ThSanctionIds.Contains(personId))
                hit = true;

            // UN match — ID AND name
            if (!hit && ctx.SanctionData.UnSanctionIds.Contains(personId))
            {
                var first = group.First();
                string fullName = first.FullName.ToUpperInvariant();
                if (ctx.SanctionData.UnSanctionNameToIds.TryGetValue(personId, out var nameSet))
                {
                    foreach (var alias in nameSet)
                    {
                        if (fullName.Contains(alias.ToUpperInvariant()) ||
                            alias.ToUpperInvariant().Contains(fullName))
                        {
                            hit = true;
                            break;
                        }
                    }
                }
            }

            if (!hit) continue;

            sbe.AddRange(SbeRecordFactory.From(group.ToList(), RuleCode, ctx));
        }

        ctx.LogCallback($"Rule 101: พบ {sbe.Count} รายการที่ตรงกับ Sanction List");
        return (sbe, new List<SaeRecord>());
    }
}
