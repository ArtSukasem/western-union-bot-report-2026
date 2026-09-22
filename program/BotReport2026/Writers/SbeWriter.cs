using BotReport2026.Models;

namespace BotReport2026.Writers;

/// <summary>
/// Writes DS_SBE (Data Set 1.1) in the 23-field layout from requirements v3,
/// one row per MTCN, straight to the .csv the ธปท. upload takes.
///
/// Unlike DS_SAE there is nothing for staff to fill in here — every field is
/// derived — so there is no intermediate .xlsx step.
/// </summary>
public static class SbeWriter
{
    /// <summary>
    /// Field names 1–23, in spec order. Reference only — the submission CSV carries
    /// <b>no header row</b>, so these are never written out.
    /// </summary>
    public static readonly string[] Headers =
    {
        "รหัสสถาบัน",
        "งวดข้อมูล",
        "เลขที่อ้างอิง",
        "ลักษณะพฤติกรรม",
        "คำอธิบายพฤติกรรม และเหตุอันควรสงสัย",
        "ประเภทของเลขที่อ้างอิงบุคคล/นิติบุคคล",
        "ประเทศที่ออกเลขที่อ้างอิงบุคคล/นิติบุคคล",
        "เลขที่อ้างอิงบุคคล/นิติบุคคล",
        "Flag ประเภทบุคคล",
        "คำนำหน้าชื่อ",
        "ชื่อ",
        "ชื่อกลาง",
        "นามสกุล",
        "ที่อยู่ปัจจุบัน",
        "รหัสไปรษณีย์ของที่อยู่ปัจจุบัน",
        "อาชีพ",
        "ประเภทธุรกิจหลัก",
        "รายได้ต่อเดือน",
        "ทุนจดทะเบียน",
        "รายละเอียดบัญชีที่พบความผิดปกติ",
        "Flag บัญชีเงินฝากธนาคาร",
        "Flag บัญชีเงินอิเล็กทรอนิกส์ (e-Money)",
        "วันที่พบความผิดปกติ"
    };

    /// <summary>Field values 1–23 for one record, in spec order.</summary>
    public static string[] ToFields(SbeRecord r) => new[]
    {
        r.InstitutionCode, r.DataPeriod, r.ReferenceNo, r.BehaviorType, r.BehaviorDescription,
        r.IdTypeCode, r.IdIssuerCountryCode, r.PersonRefId, r.FlagPersonType, r.Title,
        r.FirstName, r.MiddleName, r.LastName, r.Address, r.PostalCode,
        r.OccupationCode, r.BusinessTypeCode, r.MonthlyIncome, r.RegisteredCapital,
        r.AccountDetail, r.FlagSavingsAccount, r.FlagEMoney, r.AnomalyDate
    };

    /// <summary>
    /// Writes the submission CSV — data rows only, <b>no header row</b>: the upload
    /// takes the 23 fields by position.
    /// </summary>
    public static void Write(List<SbeRecord> records, string outputPath) =>
        CsvFile.Write(records.Select(ToFields), outputPath);
}
