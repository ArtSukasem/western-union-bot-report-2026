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
            BehaviorType = BehaviorTypeFor(behaviorType),
            BehaviorDescription = Describe(behaviorType, ctx),
            IdTypeCode = ClassificationTables.ResolveIdType(t.IdType),
            IdIssuerCountryCode = ctx.ReferenceData.ToIso2(t.IdIssuerCountry),
            PersonRefId = t.PersonId.Trim(),
            FirstName = t.FirstName,
            LastName = t.LastName,
            Address = t.FullAddress,
            PostalCode = t.PostalCode,
            OccupationCode = ResolveOccupationCode(t, ctx),
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

    /// <summary>
    /// DS_SBE field 16 — the BoT code of the occupation lookup row the transaction
    /// resolves to (see <see cref="OccupationLookup.Resolve"/>); "อื่น ๆ" when nothing matches
    /// or the matched row has no code.
    /// </summary>
    private static string ResolveOccupationCode(RspTransaction t, RuleContext ctx)
    {
        var match = ctx.Occupations.Resolve(t.Occupation, t.OccupationOther);
        return match is { Entry.BotCode.Length: > 0 }
            ? match.Entry.BotCode
            : ClassificationTables.OccupationOther;
    }

    /// <summary>"203-Retail-IB" → "203". Field 4 takes only the three-digit code.</summary>
    private static string BareRuleNumber(string ruleCode)
    {
        if (string.IsNullOrWhiteSpace(ruleCode)) return "";
        int cut = ruleCode.IndexOf('-');
        return cut < 0 ? ruleCode : ruleCode.Substring(0, cut);
    }

    /// <summary>DS_SBE field 4 — the rule number, except Rule 101 which reports as "101;211".</summary>
    private static string BehaviorTypeFor(string ruleNumber) =>
        ruleNumber == "101" ? ClassificationTables.Rule101BehaviorType : ruleNumber;

    /// <summary>DS_SBE field 5 — from behavior_descriptions.xlsx, except Rule 101's combined text.</summary>
    private static string Describe(string ruleNumber, RuleContext ctx) =>
        ruleNumber == "101"
            ? ClassificationTables.Rule101BehaviorDescription
            : ctx.ReferenceData.BehaviorDescriptions.TryGetValue(ruleNumber, out var d) ? d : "";
}
