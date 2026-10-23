using BotReport2026.Models;
using BotReport2026.Readers;

namespace BotReport2026.Rules;

/// <summary>
/// Derives the DS_SAE row that accompanies each DS_SBE row. Per requirements v3 every
/// reported behaviour needs an EDD result, so this runs for all rules — not only
/// Rule 203-Online as in the previous version.
/// </summary>
public static class SaeRecordFactory
{
    public static SaeRecord From(SbeRecord sbe, RuleContext ctx)
    {
        var t = sbe.Source;
        bool isRule101 = sbe.BehaviorType == "101";

        var sae = new SaeRecord
        {
            DataPeriod = sbe.DataPeriod,
            ReferenceNo = sbe.ReferenceNo,
            SuspiciousStartDate = sbe.AnomalyDate,
            PersonDetail = sbe.PersonDetail,
            IsPreAnswered = isRule101
        };

        if (isRule101)
        {
            // On the CFR list is by definition an abnormal behaviour; the sheet says to
            // pre-select result "1" and fill the reason, leaving nothing for staff.
            sae.EddResult = ClassificationTables.Rule101EddResultOption;
            sae.EddReason = ClassificationTables.Rule101EddReason;
            sae.SuspiciousEndDate = ctx.ReportDate;
        }

        if (t == null)
        {
            // Rule 203-Online: no RSP row, so nationality / branch / amount are unknown.
            // Sheet (A) says these transactions have already cleared EDD (CSC called the
            // customer and the transaction was then approved), so the result is pre-filled
            // as "not abnormal" — but left editable, since only Rule 101 is locked.
            if (sbe.RuleCode == "203-Online")
            {
                sae.EddResult = ClassificationTables.EddResultOptions[0];
                sae.EddCompletionDate = ctx.ReportDate;
            }
            return sae;
        }

        sae.CounterpartyTypeCode = ctx.ReferenceData.ResolveCounterpartyType(t.Nationality);
        sae.TransactionTypeCode = t.Direction == "IB"
            ? ClassificationTables.TxnTypeInbound
            : ClassificationTables.TxnTypeOutbound;

        bool isOnlineBranch = ctx.Config.SaeOnlineChannelBranches
            .Any(b => b.Trim().Equals(t.BranchAccountId.Trim(), StringComparison.OrdinalIgnoreCase));

        sae.ChannelCode = isOnlineBranch
            ? ClassificationTables.ChannelOnline
            : ClassificationTables.ChannelBranch;

        sae.TotalAmountThb = t.Principal;

        // Field 15: online channels have no physical location, and an unknown branch
        // is left blank rather than guessed.
        if (!isOnlineBranch &&
            ctx.ReferenceData.BranchAddresses.TryGetValue(
                t.BranchAccountId.Trim().ToUpperInvariant(), out var branch))
        {
            sae.TransactionLocation = branch.Combined;
        }

        return sae;
    }
}
