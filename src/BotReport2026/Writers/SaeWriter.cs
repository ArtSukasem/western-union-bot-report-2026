using BotReport2026.Models;
using BotReport2026.Readers;
using OfficeOpenXml;
using OfficeOpenXml.Drawing.Controls;

namespace BotReport2026.Writers;

/// <summary>
/// Writes DS_SAE (Data Set 1.2) in the 16-field layout from requirements v3, as a
/// macro-enabled workbook staff fill in after EDD.
///
/// Fields 5, 6, 7 and 9 are theirs to complete, so the sheet ships with list
/// validations on 6 and 7 and a free-text helper column (7.1) immediately after
/// field 7 — "the next column", per the spec.
///
/// Row 1 is a toolbar row holding the export button, so headers are on row 2 and
/// data starts on row 3; the frozen pane keeps the button on screen. The export is
/// VBA (<see cref="SaeVbaMacro"/>) rather than a feature of the generator, so staff
/// can finish the EDD columns and export without leaving Excel.
///
/// Rule 101 rows are pre-answered and their ผลการตรวจสอบ cell is locked; every other
/// cell is unlocked so sheet protection does not get in the staff's way.
/// </summary>
public static class SaeWriter
{
    /// <summary>Column index (1-based) of the 7.1 free-text helper.</summary>
    public const int OtherReasonColumn = 8;

    /// <summary>Row holding the field names. Row 1 is the button toolbar.</summary>
    public const int HeaderRow = 2;

    private const int FirstDataRow = HeaderRow + 1;
    private const int AmountColumn = 13;
    private const string SheetName = "DS_SAE";

    /// <summary>Worksheet holding the dropdown sources; hidden from staff.</summary>
    private const string OptionsSheet = "_options";

    public static readonly string[] Headers =
    {
        "รหัสสถาบัน",
        "งวดข้อมูล",
        "เลขที่อ้างอิง",
        "ประเภทบุคคล/นิติบุคคล",
        "วันที่ EDD เสร็จสิ้น",
        "ผลการตรวจสอบ",
        "คำอธิบายพฤติกรรมที่พบเพิ่มเติมภายหลัง EDD",
        "ระบุเหตุผล (กรณีเลือก อื่น ๆ)",   // 7.1 — dropped on CSV export
        "วันที่เริ่มพฤติกรรมต้องสงสัย",
        "วันที่สิ้นสุดพฤติกรรมต้องสงสัย",
        "ประเภทธุรกรรมที่ทำ",
        "ช่องทางธุรกรรม",
        "มูลค่ารวมของทุกธุรกรรม สกุลเงินบาท",
        "มูลค่ารวมของทุกธุรกรรม สกุลเงินต่างประเทศ",
        "รายละเอียดผู้ทำธุรกรรม และเลขที่อ้างอิงบุคคล",
        "สถานที่ทำธุรกรรม",
        "รายละเอียดของบัญชีที่เกี่ยวข้อง"
    };

    /// <summary>
    /// Worksheet column values including the 7.1 helper. Field 12 (amount) is the one
    /// numeric column and is written separately by <see cref="Write"/>.
    /// </summary>
    private static string[] ToFields(SaeRecord r) => new[]
    {
        r.InstitutionCode, r.DataPeriod, r.ReferenceNo, r.CounterpartyTypeCode,
        r.EddCompletionDate, r.EddResult, r.EddReason, r.EddReasonOtherText,
        r.SuspiciousStartDate, r.SuspiciousEndDate, r.TransactionTypeCode, r.ChannelCode,
        "", r.TotalAmountForeign, r.PersonDetail, r.TransactionLocation,
        r.RelatedAccountDetail
    };

    public static void Write(List<SaeRecord> records, string outputPath, ReferenceData reference)
    {
        using var pkg = new ExcelPackage();
        var ws = pkg.Workbook.Worksheets.Add(SheetName);

        for (int i = 0; i < Headers.Length; i++)
            ws.Cells[HeaderRow, i + 1].Value = Headers[i];
        WriterStyle.StyleHeaderRow(ws, HeaderRow, Headers.Length);

        for (int i = 0; i < records.Count; i++)
        {
            var r = records[i];
            var fields = ToFields(r);
            int row = FirstDataRow + i;

            for (int c = 0; c < fields.Length; c++)
            {
                if (c + 1 == AmountColumn) continue;
                ws.Cells[row, c + 1].Style.Numberformat.Format = "@";
                ws.Cells[row, c + 1].Value = fields[c];
            }

            if (r.TotalAmountThb.HasValue)
            {
                ws.Cells[row, AmountColumn].Style.Numberformat.Format = "#,##0.00";
                ws.Cells[row, AmountColumn].Value = r.TotalAmountThb.Value;
            }
        }

        if (records.Count > 0)
        {
            AddOptionsSheet(pkg, reference);
            AddValidations(ws, records.Count, reference);
            // Must come before ApplyProtection: Excel lets a protected sheet *use* an
            // autofilter (AllowAutoFilter) but never *create* one, so a filter added
            // after protection would be dead. Staff sort/filter by rule and result
            // while working through the EDD columns.
            AddAutoFilter(ws, records.Count);
            ApplyProtection(ws, records);
        }

        ws.View.FreezePanes(FirstDataRow, 1);
        ws.Cells[ws.Dimension?.Address ?? "A1"].AutoFitColumns();
        // Free text and address get long; cap them so the sheet stays readable.
        ws.Column(7).Width = Math.Min(ws.Column(7).Width, 55);
        ws.Column(OtherReasonColumn).Width = 40;
        ws.Column(16).Width = Math.Min(ws.Column(16).Width, 55);
        ws.Row(1).Height = 30;

        AddExportButton(pkg, ws);

        pkg.SaveAs(new FileInfo(outputPath));
    }

    /// <summary>
    /// Embeds the export macro and wires it to a form-control button on the toolbar
    /// row. The workbook must be saved as .xlsm for Excel to keep the VBA project.
    /// </summary>
    private static void AddExportButton(ExcelPackage pkg, ExcelWorksheet ws)
    {
        pkg.Workbook.CreateVBAProject();
        var module = pkg.Workbook.VbaProject.Modules.AddModule(SaeVbaMacro.ModuleName);
        module.Code = SaeVbaMacro.Build(SheetName, HeaderRow, Headers.Length, OtherReasonColumn);

        var button = (ExcelControlButton)ws.Drawings.AddControl(
            "btnExportCsv", eControlType.Button);
        button.Text = "⬇  ส่งออก CSV สำหรับ ธปท.";
        button.Macro = $"{SaeVbaMacro.ModuleName}.{SaeVbaMacro.EntryPoint}";
        button.SetPosition(0, 2, 0, 2);
        button.SetSize(210, 26);
        button.Locked = false;
        button.Print = false;
    }

    /// <summary>
    /// Dropdown sources live on a hidden sheet rather than inline: the EDD reasons run
    /// well past the 255-character limit Excel imposes on an inline validation list.
    /// </summary>
    private static void AddOptionsSheet(ExcelPackage pkg, ReferenceData reference)
    {
        var ws = pkg.Workbook.Worksheets.Add(OptionsSheet);
        for (int i = 0; i < ClassificationTables.EddResultOptions.Length; i++)
            ws.Cells[i + 1, 1].Value = ClassificationTables.EddResultOptions[i];
        for (int i = 0; i < reference.EddReasons.Count; i++)
            ws.Cells[i + 1, 2].Value = reference.EddReasons[i];
        ws.Hidden = eWorkSheetHidden.Hidden;
    }

    private static void AddValidations(ExcelWorksheet ws, int rowCount, ReferenceData reference)
    {
        int lastRow = FirstDataRow + rowCount - 1;

        var result = ws.DataValidations.AddListValidation($"F{FirstDataRow}:F{lastRow}");
        result.Formula.ExcelFormula =
            $"'{OptionsSheet}'!$A$1:$A${ClassificationTables.EddResultOptions.Length}";
        result.ShowErrorMessage = true;
        result.ErrorTitle = "ผลการตรวจสอบ";
        result.Error = "กรุณาเลือกจากรายการ";

        if (reference.EddReasons.Count == 0) return;

        var reason = ws.DataValidations.AddListValidation($"G{FirstDataRow}:G{lastRow}");
        reason.Formula.ExcelFormula =
            $"'{OptionsSheet}'!$B$1:$B${reference.EddReasons.Count}";
        reason.ShowErrorMessage = true;
        reason.ErrorTitle = "คำอธิบายพฤติกรรม";
        reason.Error = "กรุณาเลือกจากรายการ";
    }

    /// <summary>
    /// Puts filter buttons on the header row, covering the header plus every data row.
    /// The range stops at the last written column so the buttons line up with the 17
    /// fields rather than with whatever Excel guesses the used range to be.
    /// </summary>
    private static void AddAutoFilter(ExcelWorksheet ws, int rowCount)
    {
        int lastRow = FirstDataRow + rowCount - 1;
        ws.Cells[HeaderRow, 1, lastRow, Headers.Length].AutoFilter = true;
    }

    /// <summary>
    /// Protects only what must not change: the Rule 101 ผลการตรวจสอบ cells. Excel locks
    /// every cell by default, so the used range is unlocked first.
    /// </summary>
    private static void ApplyProtection(ExcelWorksheet ws, List<SaeRecord> records)
    {
        int lastRow = FirstDataRow + records.Count - 1;
        ws.Cells[1, 1, lastRow, Headers.Length].Style.Locked = false;

        for (int i = 0; i < records.Count; i++)
        {
            if (records[i].IsPreAnswered)
                ws.Cells[FirstDataRow + i, 6].Style.Locked = true;
        }

        ws.Protection.IsProtected = true;
        ws.Protection.AllowSelectLockedCells = true;
        ws.Protection.AllowSelectUnlockedCells = true;
        ws.Protection.AllowEditObject = true;   // keeps the export button clickable
        ws.Protection.AllowAutoFilter = true;
        ws.Protection.AllowSort = true;
    }
}
