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

    /// <summary>Rule 101 rows are pre-answered: on the CFR list is by definition abnormal.</summary>
    public const string Rule101EddResultOption = "1 = ตรวจสอบแล้ว พบว่าเป็นพฤติกรรมที่ผิดปกติ";
    public const string Rule101EddReason = "รายชื่ออยู่ใน CFR";

    /// <summary>Marker prefix for the free-text "other" option in the reason dropdown.</summary>
    public const string EddReasonOtherPrefix = "อื่น ๆ";

    // ---- Occupation Code (DS_SBE field 16) ----
    // Keys are the occupation names in sheet 'Occupation code' column A; codes are column B.
    // Anything not listed falls through to OccupationOther per the sheet's
    // "ที่เหลือที่ไม่มีระบุให้เป็นอื่น ๆ".
    public const string OccupationOther = "2003500015";

    private static readonly Dictionary<string, string> OccupationCodes =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Law Enforcement/Military Professional"] = "2003500001",
            ["Teacher/Educator"] = "2003500005",
            ["Medical and Health Care Professional"] = "2003500007",
            ["Airline/Maritime Employee"] = "2003500008",
            ["Laborer-Agriculture"] = "2003500009",
            ["Sales/Insurance/Real Estate Professional"] = "2003500010",
            ["Laborer-Manufacturing"] = "2003500011",
            ["Domestic Helper"] = "2003500012",
            ["Housewife/Child Care"] = "2003500012",
            ["Domestic Helper /Housewife/Child Care"] = "2003500012",
            ["Student"] = "2003500013",
            ["Retired"] = "2003500014",
        };

    /// <summary>
    /// Maps an RSP occupation string (cl89 / cl54) to a BoT occupation code.
    /// RSP spells occupations differently from the sheet, so an exact match is tried
    /// first, then a case-insensitive contains-match in either direction.
    /// </summary>
    public static string ResolveOccupation(string rawOccupation)
    {
        if (string.IsNullOrWhiteSpace(rawOccupation)) return OccupationOther;
        string norm = rawOccupation.Trim();

        if (OccupationCodes.TryGetValue(norm, out var code)) return code;

        // Substring match both ways, since RSP and the sheet spell occupations
        // differently ("HOUSEWIFE" vs "Housewife/Child Care"). Short values are
        // excluded from the reverse direction — a two-character occupation would
        // otherwise match almost any key.
        string upper = norm.ToUpperInvariant();
        foreach (var kv in OccupationCodes)
        {
            string key = kv.Key.ToUpperInvariant();
            if (upper.Contains(key)) return kv.Value;
            if (upper.Length >= 4 && key.Contains(upper)) return kv.Value;
        }

        return OccupationOther;
    }
}
