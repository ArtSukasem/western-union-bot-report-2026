using BotReport2026.Models;
using OfficeOpenXml;
using OfficeOpenXml.Style;

namespace BotReport2026.Writers;

public static class SaeWriter
{
    private static readonly string[] Headers =
    {
        "เลขที่อ้างอิง", "งวดข้อมูล", "ประเภทบุคคล",
        "วันที่ EDD เสร็จสิ้น", "ผลการตรวจสอบ", "รายละเอียดผู้ทำธุรกรรม"
    };

    public static void Write(List<SaeRecord> records, string outputPath)
    {
        using var pkg = new ExcelPackage();
        var ws = pkg.Workbook.Worksheets.Add("DS_SAE");

        for (int i = 0; i < Headers.Length; i++)
        {
            ws.Cells[1, i + 1].Value = Headers[i];
            ws.Cells[1, i + 1].Style.Font.Bold = true;
            ws.Cells[1, i + 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
            ws.Cells[1, i + 1].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(68, 114, 196));
            ws.Cells[1, i + 1].Style.Font.Color.SetColor(System.Drawing.Color.White);
        }

        for (int i = 0; i < records.Count; i++)
        {
            var r = records[i];
            int row = i + 2;
            ws.Cells[row, 1].Value = r.ReferenceNo;
            ws.Cells[row, 2].Value = r.ReportingPeriod;
            ws.Cells[row, 3].Value = r.PersonType;
            ws.Cells[row, 4].Value = r.EddCompletionDate.HasValue
                ? r.EddCompletionDate.Value.ToString("dd/MM/yyyy") : "";
            ws.Cells[row, 5].Value = r.EddResult;
            ws.Cells[row, 6].Value = r.PersonDetail;
        }

        ws.View.FreezePanes(2, 1);
        ws.Cells[ws.Dimension?.Address ?? "A1"].AutoFitColumns();

        pkg.SaveAs(new FileInfo(outputPath));
    }
}
