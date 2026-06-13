using BotReport2026.Models;
using BotReport2026.Readers;

namespace BotReport2026.Rules;

public class Rule203IncomeMismatch : IRuleEngine
{
    public string RuleCode => "203";

    public (List<SbeRecord>, List<SaeRecord>) Execute(RuleContext ctx)
    {
        var sbeList = new List<SbeRecord>();
        var saeList = new List<SaeRecord>();
        var period = ctx.ReportingMonth.ToString("yyyy-MM");

        // --- 203 Online: Transaction Report rows (already pre-filtered by reader) ---
        foreach (var row in ctx.TransactionReport)
        {
            sbeList.Add(new SbeRecord
            {
                ReportingPeriod = period,
                FirstName = row.SenderName,
                LastName = "",
                PersonRefId = row.SenderIdNumber,
                AnomalyDate = ctx.ReportingMonth,
                BehaviorType = "203",
                RuleCode = "203-Online",
                MtcnList = row.MTCN,
                TransactionCount = 1,
                TotalAmount = row.PrincipalAmount ?? 0,
                HasSae = true
            });
            saeList.Add(new SaeRecord
            {
                ReportingPeriod = period,
                PersonDetail = row.SenderName,
                EddCompletionDate = ctx.ReportingMonth,
                EddResult = "ผ่าน EDD"
            });
        }

        // --- 203 Retail: RSP excluding online branches ---
        var excludedBranches = ctx.Config.Rule203ExcludedBranches
            .Select(b => b.Trim().ToUpperInvariant())
            .ToHashSet();

        // Combine reporting + historical, exclude online branches
        var allRetail = ctx.ReportingMonthRsp.Concat(ctx.HistoricalRsp)
            .Where(t => !excludedBranches.Contains(t.BranchAccountId.ToUpperInvariant()))
            .Where(t => t.TransactionDate.HasValue)
            .ToList();

        // Determine rolling window cutoff: reporting month + prior N months
        var cutoff = ctx.ReportingMonth.AddMonths(-ctx.Config.Rule203RollingMonths);

        var inWindow = allRetail
            .Where(t => t.TransactionDate >= cutoff)
            .ToList();

        // Group by personId
        var byPerson = inWindow
            .Where(t => !string.IsNullOrWhiteSpace(t.PersonId))
            .GroupBy(t => t.PersonId.Trim(), StringComparer.OrdinalIgnoreCase);

        foreach (var group in byPerson)
        {
            string personId = group.Key;
            // Get occupation from any transaction in the group
            string occ = group.FirstOrDefault(t => !string.IsNullOrWhiteSpace(t.Occupation))?.Occupation ?? "";
            decimal? expectedIncome = OccupationReader.FindExpectedIncome(occ, ctx.OccupationMap);

            if (expectedIncome == null || expectedIncome <= 0)
            {
                if (!string.IsNullOrWhiteSpace(occ))
                    ctx.LogCallback($"Rule 203 Retail: ไม่พบรายได้สำหรับอาชีพ '{occ}' (PersonId: {personId})");
                continue;
            }

            decimal threshold = expectedIncome.Value * ctx.Config.Rule203Multiplier;
            decimal windowTotal = group.Sum(t => t.Principal);

            if (windowTotal <= threshold) continue;

            // Flag only reporting-month transactions for this person
            var reportingTxns = ctx.ReportingMonthRsp
                .Where(t => !excludedBranches.Contains(t.BranchAccountId.ToUpperInvariant()))
                .Where(t => t.PersonId.Equals(personId, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (reportingTxns.Count == 0) continue;

            var first = reportingTxns[0];
            sbeList.Add(new SbeRecord
            {
                ReportingPeriod = period,
                FirstName = first.FirstName,
                LastName = first.LastName,
                PersonRefId = personId,
                MonthlyIncome = expectedIncome,
                AnomalyDate = reportingTxns.Min(t => t.TransactionDate),
                BehaviorType = "203",
                RuleCode = "203-Retail",
                MtcnList = string.Join(", ", reportingTxns.Select(t => t.MTCN).Distinct()),
                TransactionCount = reportingTxns.Count,
                TotalAmount = reportingTxns.Sum(t => t.Principal)
            });
        }

        ctx.LogCallback($"Rule 203: Online={ctx.TransactionReport.Count}, Retail={sbeList.Count - ctx.TransactionReport.Count} รายการ");
        return (sbeList, saeList);
    }
}
