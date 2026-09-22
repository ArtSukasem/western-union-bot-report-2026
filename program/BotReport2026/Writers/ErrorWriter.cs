using OfficeOpenXml;

namespace BotReport2026.Writers;

/// <summary>
/// A data-quality problem found while building the report.
/// <paramref name="SourceFile"/> and <paramref name="SourceRow"/> point at the input
/// row so the value can actually be located and corrected; both are blank/0 when the
/// record did not come from an RSP file (Rule 203-Online has no RSP row behind it).
/// </summary>
public record ReportError(
    string MTCN, string Value, string Message, string SourceFile, int SourceRow);

/// <summary>
/// Writes error.xlsx — the validation warnings required by DS_SBE field 8, where a
/// citizen ID that is not exactly 13 digits must be reported. These are warnings, not
/// filters: the affected rows still appear in DS_SBE.
/// </summary>
public static class ErrorWriter
{
    private static readonly string[] Headers =
        { "MTCN", "Citizen ID", "Error Message", "Source File", "Row" };

    public const string InvalidCitizenId = "Citizen Id is invalid";

    /// <summary>
    /// Writes the file when there is anything to report; deletes any stale copy from a
    /// previous run otherwise, so a leftover file never looks like a fresh failure.
    /// </summary>
    public static bool Write(List<ReportError> errors, string outputPath)
    {
        if (errors.Count == 0)
        {
            if (File.Exists(outputPath)) File.Delete(outputPath);
            return false;
        }

        using var pkg = new ExcelPackage();
        var ws = pkg.Workbook.Worksheets.Add("Errors");
        WriterStyle.WriteHeader(ws, Headers);

        for (int i = 0; i < errors.Count; i++)
        {
            var e = errors[i];
            int row = i + 2;
            // Text format so MTCNs and IDs keep their leading zeros.
            ws.Cells[row, 1, row, 4].Style.Numberformat.Format = "@";
            ws.Cells[row, 1].Value = e.MTCN;
            ws.Cells[row, 2].Value = e.Value;
            ws.Cells[row, 3].Value = e.Message;
            ws.Cells[row, 4].Value = e.SourceFile;
            if (e.SourceRow > 0) ws.Cells[row, 5].Value = e.SourceRow;
        }

        ws.View.FreezePanes(2, 1);
        ws.Cells[ws.Dimension?.Address ?? "A1"].AutoFitColumns();
        pkg.SaveAs(new FileInfo(outputPath));
        return true;
    }
}
