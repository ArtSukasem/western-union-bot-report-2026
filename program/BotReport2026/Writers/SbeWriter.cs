using BotReport2026.Models;
using OfficeOpenXml;
using OfficeOpenXml.Style;

namespace BotReport2026.Writers;

public static class SbeWriter
{
    private static readonly string[] Headers =
    {
        "งวดข้อมูล", "เลขที่อ้างอิง", "ประเภทบุคคล", "Flag_ประเภทบุคคล",
        "คำนำหน้า", "ชื่อ", "นามสกุล", "เลขที่อ้างอิงบุคคล",
        "รายได้ต่อเดือน", "รายละเอียดบัญชี", "Flag_บัญชีเงินฝาก", "Flag_e-Money",
        "วันที่พบความผิดปกติ", "ประเภทพฤติกรรม", "รายการธุรกรรม (MTCN)",
        "จำนวนครั้ง", "ยอดเงินรวม"
    };

    public static void Write(List<SbeRecord> records, string outputPath)
    {
        using var pkg = new ExcelPackage();
        var ws = pkg.Workbook.Worksheets.Add("DS_SBE");

        // Header row
        for (int i = 0; i < Headers.Length; i++)
        {
            ws.Cells[1, i + 1].Value = Headers[i];
            ws.Cells[1, i + 1].Style.Font.Bold = true;
            ws.Cells[1, i + 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
            ws.Cells[1, i + 1].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(68, 114, 196));
            ws.Cells[1, i + 1].Style.Font.Color.SetColor(System.Drawing.Color.White);
        }

        // Data rows
        for (int i = 0; i < records.Count; i++)
        {
            var r = records[i];
            int row = i + 2;
            ws.Cells[row, 1].Value = r.ReportingPeriod;
            ws.Cells[row, 2].Value = r.ReferenceNo;
            ws.Cells[row, 3].Value = r.PersonType;
            ws.Cells[row, 4].Value = r.FlagPersonType;
            ws.Cells[row, 5].Value = r.Title;
            ws.Cells[row, 6].Value = r.FirstName;
            ws.Cells[row, 7].Value = r.LastName;
            ws.Cells[row, 8].Value = r.PersonRefId;
            ws.Cells[row, 9].Value = r.MonthlyIncome.HasValue ? (object)r.MonthlyIncome.Value : "";
            ws.Cells[row, 10].Value = r.AccountDetail;
            ws.Cells[row, 11].Value = r.FlagSavingsAccount;
            ws.Cells[row, 12].Value = r.FlagEMoney;
            ws.Cells[row, 13].Value = r.AnomalyDate.HasValue
                ? r.AnomalyDate.Value.ToString("dd/MM/yyyy") : "";
            ws.Cells[row, 14].Value = r.BehaviorType;
            ws.Cells[row, 15].Value = r.MtcnList;
            ws.Cells[row, 16].Value = r.TransactionCount;
            ws.Cells[row, 17].Value = r.TotalAmount;
        }

        // Freeze top row, auto-fit
        ws.View.FreezePanes(2, 1);
        ws.Cells[ws.Dimension?.Address ?? "A1"].AutoFitColumns();

        pkg.SaveAs(new FileInfo(outputPath));
    }
}
