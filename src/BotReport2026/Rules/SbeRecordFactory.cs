using BotReport2026.Models;
using BotReport2026.Readers;

namespace BotReport2026.Rules;

/// <summary>
/// Single place where an RSP transaction becomes a DS_SBE row. Rules decide *which*
/// transactions to report; this decides what all 23 fields contain, so the mapping
/// lives in one file rather than being repeated across eight rule classes.
/// </summary>
public static class SbeRecordFactory
{
    /// <summary>
    /// Builds one DS_SBE row per transaction.
    /// <paramref name="ruleCode"/> may carry a -IB/-OB suffix for logging; field 4
    /// always gets the bare rule number.
    /// </summary>
    public static IEnumerable<SbeRecord> From(
        IEnumerable<RspTransaction> txns, string ruleCode, RuleContext ctx) =>
        txns.Select(t => From(t, ruleCode, ctx));

    public static SbeRecord From(RspTransaction t, string ruleCode, RuleContext ctx)
    {
        string behaviorType = BareRuleNumber(ruleCode);
        return new SbeRecord
        {
            DataPeriod = ctx.DataPeriod,
            ReferenceNo = t.MTCN,
            BehaviorType = behaviorType,
            BehaviorDescription = Describe(behaviorType, ctx),
            IdTypeCode = ClassificationTables.ResolveIdType(t.IdType),
            IdIssuerCountryCode = ctx.ReferenceData.ToIso2(t.IdIssuerCountry),
            PersonRefId = t.PersonId.Trim(),
            FirstName = t.FirstName,
            LastName = t.LastName,
            Address = t.FullAddress,
            PostalCode = t.PostalCode,
            OccupationCode = ClassificationTables.ResolveOccupation(t.Occupation),
            AnomalyDate = ctx.ReportDate,
            Source = t,
            RuleCode = ruleCode
        };
    }

    /// <summary>
    /// Rule 203-Online rows come from the Transaction Report, which carries no ID type,
    /// nationality or address — those fields stay blank.
    /// </summary>
    public static SbeRecord FromOnline(TransactionReportRow row, string ruleCode, RuleContext ctx)
    {
        string behaviorType = BareRuleNumber(ruleCode);
        return new SbeRecord
        {
            DataPeriod = ctx.DataPeriod,
            ReferenceNo = row.MTCN,
            BehaviorType = behaviorType,
            BehaviorDescription = Describe(behaviorType, ctx),
            PersonRefId = row.SenderIdNumber.Trim(),
            FirstName = row.SenderName,
            AnomalyDate = ctx.ReportDate,
            RuleCode = ruleCode
        };
    }

    /// <summary>"203-Retail-IB" → "203". Field 4 takes only the three-digit code.</summary>
    private static string BareRuleNumber(string ruleCode)
    {
        if (string.IsNullOrWhiteSpace(ruleCode)) return "";
        int cut = ruleCode.IndexOf('-');
        return cut < 0 ? ruleCode : ruleCode.Substring(0, cut);
    }

    private static string Describe(string behaviorType, RuleContext ctx) =>
        ctx.ReferenceData.BehaviorDescriptions.TryGetValue(behaviorType, out var d) ? d : "";
}
