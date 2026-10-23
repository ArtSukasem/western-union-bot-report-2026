namespace BotReport2026.Readers;

/// <summary>
/// Fixed BoT classification enumerations for DS_SBE / DS_SAE (requirements v3).
/// These are stable BoT code lists, so they live in code rather than in a lookup file —
/// unlike branch addresses and country codes, which change and are read from SAE-lookups/.
/// </summary>
public static class ClassificationTables
{
    /// <summary>DS_SBE/DS_SAE field 1 — บจ.สรรพสินค้าเซ็นทรัล.</summary>
    public const string InstitutionCode = "A39";

    /// <summary>DS_SBE field 9 — "1" = บุคคลธรรมดา.</summary>
    public const string FlagPersonTypeIndividual = "1";

    /// <summary>DS_SBE fields 21 & 22 — no bank deposit / e-Money accounts involved.</summary>
    public const string FlagNo = "0";

    // ---- Identification Type Code (DS_SBE field 6) ----
    // WU uses A = passport, B = personal ID; anything else maps to Other Code.
    public const string IdTypePersonalId = "2002700001";
    public const string IdTypePassport = "2002700002";
    public const string IdTypeOther = "2002700019";

    public static string ResolveIdType(string wuIdType) =>
        (wuIdType?.Trim().ToUpperInvariant()) switch
        {
            "B" => IdTypePersonalId,
            "A" => IdTypePassport,
            _ => IdTypeOther
        };

    // ---- Counterparty Type Code (DS_SAE field 4) ----
    public const string CounterpartyThai = "2001400001";
    public const string CounterpartyForeign = "2001400050";

    // ---- ประเภทธุรกรรมที่ทำ (DS_SAE field 10) ----
    public const string TxnTypeInbound = "0802900017";
    public const string TxnTypeOutbound = "0802900005";

    // ---- ช่องทางธุรกรรม (DS_SAE field 11) ----
    public const string ChannelOnline = "330004";
    public const string ChannelBranch = "330005";

    // ---- ผลการตรวจสอบ (DS_SAE field 6) ----
    public const string EddResultNotAbnormal = "0";
    public const string EddResultAbnormal = "1";
    public const string EddResultNotCustomer = "2";

    public static readonly string[] EddResultOptions =
    {
        "0 = ตรวจสอบแล้ว พบว่าไม่เป็นพฤติกรรมที่ผิดปกติ",
        "1 = ตรวจสอบแล้ว พบว่าเป็นพฤติกรรมที่ผิดปกติ",
        "2 = ตรวจสอบแล้ว พบว่าไม่ใช่ลูกค้าสถาบัน"
    };

    /// <summary>The stored value for a picked dropdown label (the leading digit).</summary>
    public static string EddResultValue(string option) =>
        string.IsNullOrWhiteSpace(option) ? "" : option.Trim().Substring(0, 1);

    /// <summary>
    /// DS_SBE fields 4 & 5 for a Rule 101 hit: reported as both 101 (used in crime) and
    /// 211 (linked to accounts under a seizure order), with one combined description in
    /// place of the per-code text from behavior_descriptions.xlsx.
    /// </summary>
    public const string Rule101BehaviorType = "101;211";
    public const string Rule101BehaviorDescription =
        "พบว่าธุรกรรม ถูกนำไปใช้หรือเกี่ยวข้องในการประกอบอาชญากรรมและมีความเชื่องโยงในลักษณะที่มีการทำรายการผิดปกติ " +
        "เช่น บัญชีที่ถูกออกหมายอายัด";

    /// <summary>Rule 101 rows are pre-answered: on the CFR list is by definition abnormal.</summary>
    public const string Rule101EddResultOption = "1 = ตรวจสอบแล้ว พบว่าเป็นพฤติกรรมที่ผิดปกติ";
    public const string Rule101EddReason = "รายชื่ออยู่ใน CFR";

    /// <summary>Marker prefix for the free-text "other" option in the reason dropdown.</summary>
    public const string EddReasonOtherPrefix = "อื่น ๆ";

    // ---- Occupation Code (DS_SBE field 16) ----
    // The code itself comes from the "BoT code" column of the occupation lookup
    // (lookups/list_of_occupation_expected_monthly_income.xlsx); this is the fallback for
    // an occupation that matches no row, per the sheet's "ที่เหลือที่ไม่มีระบุให้เป็นอื่น ๆ".
    public const string OccupationOther = "2003500015";
}
