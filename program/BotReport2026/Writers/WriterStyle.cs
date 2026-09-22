using OfficeOpenXml;
using OfficeOpenXml.Style;

namespace BotReport2026.Writers;

/// <summary>Shared header styling so every output workbook looks the same.</summary>
public static class WriterStyle
{
    private static readonly System.Drawing.Color HeaderFill =
        System.Drawing.Color.FromArgb(68, 114, 196);

    public static void WriteHeader(ExcelWorksheet ws, IReadOnlyList<string> headers)
    {
        for (int i = 0; i < headers.Count; i++)
            ws.Cells[1, i + 1].Value = headers[i];
        StyleHeaderRow(ws, 1, headers.Count);
    }

    /// <summary>Styles an already-populated header row (DS_SAE puts it on row 2).</summary>
    public static void StyleHeaderRow(ExcelWorksheet ws, int row, int columnCount)
    {
        for (int i = 0; i < columnCount; i++)
        {
            var cell = ws.Cells[row, i + 1];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.PatternType = ExcelFillStyle.Solid;
            cell.Style.Fill.BackgroundColor.SetColor(HeaderFill);
            cell.Style.Font.Color.SetColor(System.Drawing.Color.White);
        }
    }
}
