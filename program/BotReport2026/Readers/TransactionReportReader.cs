using BotReport2026.Models;
using OfficeOpenXml;

namespace BotReport2026.Readers;

public static class TransactionReportReader
{
    private static readonly HashSet<string> QualifyingStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Approved", "Received", "Delivered"
    };

    /// <summary>
    /// Reads Transaction Report file.
    /// Columns are indexed by position (cl3=MTCN, cl4=Status, cl9=Sender name,
    /// cl12=Sender ID number, cl17=Principal Amount, cl24=Error reason).
    /// Pre-filters to rows where Error_Reason mentions income AND Status qualifies.
    /// </summary>
    public static List<TransactionReportRow> ReadFile(string filePath)
    {
        var results = new List<TransactionReportRow>();
        using var pkg = new ExcelPackage(new FileInfo(filePath));
        var ws = pkg.Workbook.Worksheets[0];
        int totalRows = ws.Dimension?.Rows ?? 0;

        int firstDataRow = FindFirstDataRow(ws, totalRows);
        if (firstDataRow > totalRows) return results;

        for (int row = firstDataRow; row <= totalRows; row++)
        {
            string Get(int col) => ws.Cells[row, col].Text?.Trim() ?? "";

            string errorReason = Get(24); // cl24
            string status = Get(4);       // cl4

            // Only include income-flagged rows with EDD-passed status
            if (!errorReason.StartsWith("Income", StringComparison.OrdinalIgnoreCase) &&
                !errorReason.Contains("income", StringComparison.OrdinalIgnoreCase))
                continue;

            if (!QualifyingStatuses.Contains(status)) continue;

            results.Add(new TransactionReportRow
            {
                MTCN = Get(3),
                Status = status,
                SenderName = Get(9),
                SenderIdNumber = Get(12),
                PrincipalAmount = RspReader.ParseDecimal(Get(17)) is decimal d && d > 0 ? d : null,
                ErrorReason = errorReason
            });
        }

        return results;
    }

    /// <summary>
    /// Finds the first data row. The export has appeared with one header row (row 1 =
    /// field names) and with two (row 1 = an "online" banner, row 2 = field names), so
    /// the "MTCN" header in cl3 is located rather than assumed. Falls back to row 2.
    /// </summary>
    public static int FindFirstDataRow(ExcelWorksheet ws, int totalRows)
    {
        for (int row = 1; row <= Math.Min(3, totalRows); row++)
        {
            string cell = ws.Cells[row, 3].Text?.Trim() ?? "";
            if (cell.Equals("MTCN", StringComparison.OrdinalIgnoreCase))
                return row + 1;
        }
        return 2;
    }
}
