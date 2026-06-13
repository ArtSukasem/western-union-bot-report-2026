using BotReport2026.Models;
using OfficeOpenXml;

namespace BotReport2026.Readers;

public static class RspReader
{
    /// <summary>
    /// Reads an RSP file (Inbound or Outbound).
    /// RSP file structure: Row1=field names (single header row), Data from Row2.
    /// Columns are indexed by position (cl1..cl121); header text is ignored.
    /// </summary>
    public static List<RspTransaction> ReadFile(string filePath, string direction)
    {
        var results = new List<RspTransaction>();
        using var pkg = new ExcelPackage(new FileInfo(filePath));
        var ws = pkg.Workbook.Worksheets[0];
        int totalRows = ws.Dimension?.Rows ?? 0;
        if (totalRows < 2) return results;

        // Data starts at row 2 (1-based) — one header row
        for (int row = 2; row <= totalRows; row++)
        {
            string Get(int col) => ws.Cells[row, col].Text?.Trim() ?? "";

            string mtcn = Get(1); // cl1
            if (string.IsNullOrWhiteSpace(mtcn)) continue;

            var txn = new RspTransaction
            {
                MTCN = mtcn,
                Direction = direction,
                SourceFile = Path.GetFileName(filePath)
            };

            if (direction == "IB")
            {
                // IB: cl5=Pay_Date, cl6=Pay_Time, cl12=Pay_Principal, cl22=Branch
                // cl60=ReceiverFirstName, cl61=ReceiverLastName, cl74=ReceiverID
                // cl89=ReceiverOccupation, cl114=ReceiverOccupationOther
                txn.TransactionDate = ParseDate(Get(5));
                txn.TransactionTime = ParseTime(Get(6));
                txn.Principal = ParseDecimal(Get(12));
                txn.BranchAccountId = Get(22);
                txn.FirstName = Get(60);
                txn.LastName = Get(61);
                txn.PersonId = Get(74);
                string occ = Get(89);
                txn.Occupation = IsOther(occ) ? Get(114) : occ;
            }
            else // OB
            {
                // OB: cl3=Send_Date, cl4=Send_Time, cl8=Send_Principal, cl16=Branch
                // cl25=SenderFirstName, cl26=SenderLastName, cl39=SenderID
                // cl54=SenderOccupation, cl104=SenderOccupationOther
                txn.TransactionDate = ParseDate(Get(3));
                txn.TransactionTime = ParseTime(Get(4));
                txn.Principal = ParseDecimal(Get(8));
                txn.BranchAccountId = Get(16);
                txn.FirstName = Get(25);
                txn.LastName = Get(26);
                txn.PersonId = Get(39);
                string occ = Get(54);
                txn.Occupation = IsOther(occ) ? Get(104) : occ;
            }

            if (txn.Principal <= 0) continue;
            results.Add(txn);
        }

        return results;
    }

    private static bool IsOther(string occ) =>
        string.IsNullOrWhiteSpace(occ) ||
        occ.Equals("OTHER", StringComparison.OrdinalIgnoreCase) ||
        occ.Equals("OTHERS", StringComparison.OrdinalIgnoreCase);

    public static DateTime? ParseDate(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        string[] formats = { "yyyy-MM-dd", "dd-MM-yyyy", "dd/MM/yyyy", "M/d/yyyy", "yyyy/MM/dd" };
        if (DateTime.TryParseExact(s, formats, null,
            System.Globalization.DateTimeStyles.None, out var dt)) return dt;
        if (DateTime.TryParse(s, out dt)) return dt;
        return null;
    }

    public static TimeSpan? ParseTime(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        if (TimeSpan.TryParse(s, out var ts)) return ts;
        return null;
    }

    public static decimal ParseDecimal(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return 0;
        if (decimal.TryParse(s.Replace(",", ""),
            System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var d)) return d;
        return 0;
    }
}
