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
    /// Structure: Row1="online", Row2=field names, Data from Row3.
    /// Columns are indexed by position; header text is ignored.
    /// Pre-filters to rows where Error_Reason starts with "Income" AND Status qualifies.
    /// </summary>
    public static List<TransactionReportRow> ReadFile(string filePath)
    {
        var results = new List<TransactionReportRow>();
        using var pkg = new ExcelPackage(new FileInfo(filePath));
        var ws = pkg.Workbook.Worksheets[0];
        int totalRows = ws.Dimension?.Rows ?? 0;
        if (totalRows < 3) return results;

        for (int row = 3; row <= totalRows; row++)
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
}
