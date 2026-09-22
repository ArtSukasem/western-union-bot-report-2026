using BotReport2026.Readers;

namespace BotReport2026.Models;

/// <summary>
/// DS_SBE (Data Set 1.1) — one row per MTCN, per requirements v3 sheet '(DS_SBE)'.
/// Primary key: InstitutionCode + DataPeriod + ReferenceNo.
/// Property order matches the spec's field numbering 1–23.
/// </summary>
public class SbeRecord
{
    // 1
    public string InstitutionCode { get; set; } = ClassificationTables.InstitutionCode;
    // 2 — last day of the reporting month, YYYY-MM-DD
    public string DataPeriod { get; set; } = "";
    // 3 — MTCN (cl1)
    public string ReferenceNo { get; set; } = "";
    // 4 — rule number, e.g. "101"
    public string BehaviorType { get; set; } = "";
    // 5 — description looked up from behavior_descriptions.xlsx
    public string BehaviorDescription { get; set; } = "";
    // 6 — Identification Type Code
    public string IdTypeCode { get; set; } = "";
    // 7 — ISO2 country that issued the ID
    public string IdIssuerCountryCode { get; set; } = "";
    // 8 — the ID number itself
    public string PersonRefId { get; set; } = "";
    // 9 — "1" = บุคคลธรรมดา
    public string FlagPersonType { get; set; } = ClassificationTables.FlagPersonTypeIndividual;
    // 10 — blank
    public string Title { get; set; } = "";
    // 11
    public string FirstName { get; set; } = "";
    // 12 — blank
    public string MiddleName { get; set; } = "";
    // 13
    public string LastName { get; set; } = "";
    // 14 — address lines joined with spaces
    public string Address { get; set; } = "";
    // 15
    public string PostalCode { get; set; } = "";
    // 16 — Occupation Code
    public string OccupationCode { get; set; } = "";
    // 17–20 — blank (no juristic persons; BoT confirmed account detail may be blank)
    public string BusinessTypeCode { get; set; } = "";
    public string MonthlyIncome { get; set; } = "";
    public string RegisteredCapital { get; set; } = "";
    public string AccountDetail { get; set; } = "";
    // 21, 22 — no bank deposit / e-Money accounts
    public string FlagSavingsAccount { get; set; } = ClassificationTables.FlagNo;
    public string FlagEMoney { get; set; } = ClassificationTables.FlagNo;
    // 23 — report run date, YYYY-MM-DD
    public string AnomalyDate { get; set; } = "";

    /// <summary>
    /// The RSP row this record came from, kept so DS_SAE can be derived without a
    /// second lookup. Null for Rule 203-Online rows, which originate from the
    /// Transaction Report and have no RSP row behind them.
    /// </summary>
    public RspTransaction? Source { get; set; }

    /// <summary>Internal rule identifier including the -IB/-OB suffix, for logging only.</summary>
    public string RuleCode { get; set; } = "";

    /// <summary>DS_SAE field 14 — SBE fields 6, 7, 8, 11, 13 joined with commas.</summary>
    public string PersonDetail =>
        string.Join(",", IdTypeCode, IdIssuerCountryCode, PersonRefId,
            $"{FirstName} {LastName}".Trim());
}
