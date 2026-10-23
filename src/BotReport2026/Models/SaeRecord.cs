using BotReport2026.Readers;

namespace BotReport2026.Models;

/// <summary>
/// DS_SAE (Data Set 1.2) — one row per DS_SBE row, joined on ReferenceNo (the MTCN),
/// per requirements v3 sheet '(DS_SAE)'. Property order matches fields 1–16.
///
/// Fields 5, 6, 7 and 9 are filled in by staff after EDD; the writer emits them as
/// empty cells with Excel dropdowns / date entry rather than deriving a value.
/// </summary>
public class SaeRecord
{
    // 1
    public string InstitutionCode { get; set; } = ClassificationTables.InstitutionCode;
    // 2
    public string DataPeriod { get; set; } = "";
    // 3 — MTCN; matches SbeRecord.ReferenceNo
    public string ReferenceNo { get; set; } = "";
    // 4 — Counterparty Type Code, from nationality
    public string CounterpartyTypeCode { get; set; } = "";
    // 5 — staff: EDD completion date
    public string EddCompletionDate { get; set; } = "";
    // 6 — staff dropdown; pre-set and locked for Rule 101
    public string EddResult { get; set; } = "";
    // 7 — staff dropdown; 7.1 free text replaces it on CSV export
    public string EddReason { get; set; } = "";
    public string EddReasonOtherText { get; set; } = "";
    // 8 — วันที่เริ่มพฤติกรรมต้องสงสัย = the SBE anomaly date
    public string SuspiciousStartDate { get; set; } = "";
    // 9 — staff, except Rule 101 which takes the report date
    public string SuspiciousEndDate { get; set; } = "";
    // 10 — ประเภทธุรกรรมที่ทำ (IB / OB)
    public string TransactionTypeCode { get; set; } = "";
    // 11 — ช่องทางธุรกรรม (online / branch)
    public string ChannelCode { get; set; } = "";
    // 12
    public decimal? TotalAmountThb { get; set; }
    // 13 — blank
    public string TotalAmountForeign { get; set; } = "";
    // 14 — SBE fields 6,7,8,11,13 comma-joined
    public string PersonDetail { get; set; } = "";
    // 15 — branch address, blank for online channels
    public string TransactionLocation { get; set; } = "";
    // 16 — blank
    public string RelatedAccountDetail { get; set; } = "";

    /// <summary>Rule 101 rows are pre-answered and their result cell is locked.</summary>
    public bool IsPreAnswered { get; set; }

    /// <summary>
    /// The value that goes into field 7 on export: the free text when the staff picked
    /// "อื่น ๆ", otherwise the chosen dropdown option.
    /// </summary>
    public string ResolvedEddReason =>
        EddReason.StartsWith(ClassificationTables.EddReasonOtherPrefix, StringComparison.Ordinal)
        && !string.IsNullOrWhiteSpace(EddReasonOtherText)
            ? EddReasonOtherText.Trim()
            : EddReason;
}
