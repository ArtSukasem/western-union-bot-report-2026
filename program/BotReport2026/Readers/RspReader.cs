using BotReport2026.Models;

namespace BotReport2026.Readers;

public static class RspReader
{
    /// <summary>Statuses kept when the caller does not specify a filter.</summary>
    public static readonly string[] DefaultIncludedStatuses = { "PAID" };

    /// <summary>Rows read minus rows kept, split by the reason they were dropped.</summary>
    public record ReadResult(List<RspTransaction> Rows, int SkippedByStatus);

    /// <summary>
    /// Reads an RSP CSV file (Inbound or Outbound).
    /// RSP file structure: Row1=field names (single header row), Data from Row2.
    /// Columns are indexed by position (cl1..cl121); header text is ignored.
    /// </summary>
    public static List<RspTransaction> ReadFile(
        string filePath, string direction, ICollection<string>? includedStatuses = null) =>
        Read(filePath, direction, includedStatuses).Rows;

    /// <summary>Same as <see cref="ReadFile"/> but also reports how many rows the status filter dropped.</summary>
    public static ReadResult Read(
        string filePath, string direction, ICollection<string>? includedStatuses = null)
    {
        var statuses = new HashSet<string>(
            includedStatuses ?? DefaultIncludedStatuses, StringComparer.OrdinalIgnoreCase);
        statuses.RemoveWhere(string.IsNullOrWhiteSpace);

        var results = new List<RspTransaction>();
        int skippedByStatus = 0;
        bool isHeader = true;
        int rowNumber = 0;   // 1-based, matching what Excel shows (row 1 = header)

        foreach (var row in CsvUtil.ReadRows(filePath))
        {
            rowNumber++;
            if (isHeader) { isHeader = false; continue; }

            string Get(int col) => col <= row.Length ? CsvUtil.Clean(row[col - 1]) : "";

            string mtcn = Get(1); // cl1
            if (string.IsNullOrWhiteSpace(mtcn)) continue;

            // cl2 = Transaction_Status (PAID / CANCELLED / REFUNDED / UNPAID).
            // An empty status list means "no filtering".
            if (statuses.Count > 0 && !statuses.Contains(Get(2)))
            {
                skippedByStatus++;
                continue;
            }

            var txn = new RspTransaction
            {
                MTCN = mtcn,
                Direction = direction,
                SourceFile = Path.GetFileName(filePath),
                SourceRow = rowNumber
            };

            if (direction == "IB")
            {
                // IB: cl5=Pay_Date, cl6=Pay_Time, cl12=Pay_Principal, cl22=Branch
                // cl60=ReceiverFirstName, cl61=ReceiverLastName, cl74=ReceiverID
                // cl89=ReceiverOccupation, cl114=ReceiverOccupationOther
                // v3: cl66=Nationality, cl68-73=Address, cl77=Id1_Issuer, cl78=Id1_Type
                txn.TransactionDate = ParseDate(Get(5));
                txn.TransactionTime = ParseTime(Get(6));
                txn.Principal = ParseDecimal(Get(12));
                txn.BranchAccountId = Get(22);
                txn.FirstName = Get(60);
                txn.LastName = Get(61);
                txn.PersonId = Get(74);
                string occ = Get(89);
                txn.Occupation = IsOther(occ) ? Get(114) : occ;
                txn.Nationality = Get(66);
                txn.AddressLines = new[] { Get(68), Get(69), Get(70), Get(71), Get(72), Get(73) };
                txn.PostalCode = Get(73);
                txn.IdIssuerCountry = Get(77);
                txn.IdType = Get(78);
            }
            else // OB
            {
                // OB: cl3=Send_Date, cl4=Send_Time, cl8=Send_Principal, cl16=Branch
                // cl25=SenderFirstName, cl26=SenderLastName, cl39=SenderID
                // cl54=SenderOccupation, cl104=SenderOccupationOther
                // v3: cl31=Nationality, cl33-38=Address, cl42=Id1_Issuer, cl43=Id1_Type
                txn.TransactionDate = ParseDate(Get(3));
                txn.TransactionTime = ParseTime(Get(4));
                txn.Principal = ParseDecimal(Get(8));
                txn.BranchAccountId = Get(16);
                txn.FirstName = Get(25);
                txn.LastName = Get(26);
                txn.PersonId = Get(39);
                string occ = Get(54);
                txn.Occupation = IsOther(occ) ? Get(104) : occ;
                txn.Nationality = Get(31);
                txn.AddressLines = new[] { Get(33), Get(34), Get(35), Get(36), Get(37), Get(38) };
                txn.PostalCode = Get(38);
                txn.IdIssuerCountry = Get(42);
                txn.IdType = Get(43);
            }

            if (txn.Principal <= 0) continue;
            results.Add(txn);
        }

        return new ReadResult(results, skippedByStatus);
    }

    private static bool IsOther(string occ) =>
        string.IsNullOrWhiteSpace(occ) ||
        occ.Equals("OTHER", StringComparison.OrdinalIgnoreCase) ||
        occ.Equals("OTHERS", StringComparison.OrdinalIgnoreCase);

    public static DateTime? ParseDate(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        // RSP dates are MM/dd/yyyy — dd/MM/yyyy is deliberately absent because it
        // would swallow the same strings and silently shift day/month.
        string[] formats = { "MM/dd/yyyy", "M/d/yyyy", "yyyy-MM-dd", "yyyy/MM/dd", "dd-MM-yyyy" };
        var invariant = System.Globalization.CultureInfo.InvariantCulture;
        if (DateTime.TryParseExact(s, formats, invariant,
            System.Globalization.DateTimeStyles.None, out var dt)) return dt;
        if (DateTime.TryParse(s, invariant,
            System.Globalization.DateTimeStyles.None, out dt)) return dt;
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
