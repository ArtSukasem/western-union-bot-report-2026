using System.Text.RegularExpressions;
using OfficeOpenXml;

namespace BotReport2026.Readers;

public record InputFile(string Path, DateTime? Month, int RowCount)
{
    public string FileName => System.IO.Path.GetFileName(Path);

    public string Display
    {
        get
        {
            string monthStr = Month.HasValue ? Month.Value.ToString("MMM yyyy") : "เดือน?";
            return $"{FileName}   ({RowCount:N0} แถว · {monthStr})";
        }
    }
}

public static class InputFolderScanner
{
    private static readonly Dictionary<string, int> MonthMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Jan"] = 1, ["Feb"] = 2, ["Mar"] = 3, ["Apr"] = 4, ["May"] = 5, ["Jun"] = 6,
        ["Jul"] = 7, ["Aug"] = 8, ["Sep"] = 9, ["Oct"] = 10, ["Nov"] = 11, ["Dec"] = 12
    };

    private static readonly Regex MonthRegex =
        new(@"([A-Za-z]{3})[ _]?(\d{2})", RegexOptions.Compiled);

    /// <summary>Parses a month code like "Apr26" / "May26" from a filename.</summary>
    public static DateTime? ParseMonthFromName(string fileName)
    {
        foreach (Match m in MonthRegex.Matches(fileName))
        {
            string mon = m.Groups[1].Value;
            if (MonthMap.TryGetValue(mon, out int monthNum) &&
                int.TryParse(m.Groups[2].Value, out int yy))
            {
                int year = 2000 + yy;
                return new DateTime(year, monthNum, 1);
            }
        }
        return null;
    }

    /// <summary>Counts data rows (excluding header rows) using EPPlus dimension only.</summary>
    public static int CountDataRows(string path, int headerRows)
    {
        try
        {
            using var pkg = new ExcelPackage(new FileInfo(path));
            var ws = pkg.Workbook.Worksheets.FirstOrDefault();
            int total = ws?.Dimension?.Rows ?? 0;
            return Math.Max(0, total - headerRows);
        }
        catch { return 0; }
    }

    /// <summary>Scans RSP folder; returns (inbound, outbound) lists.</summary>
    public static (List<InputFile> inbound, List<InputFile> outbound) ScanRsp(string dir)
    {
        var inbound = new List<InputFile>();
        var outbound = new List<InputFile>();
        if (!Directory.Exists(dir)) return (inbound, outbound);

        foreach (var path in Directory.GetFiles(dir))
        {
            string name = Path.GetFileName(path);
            if (name.StartsWith("~$")) continue; // skip Excel temp locks

            if (name.StartsWith("RSP_Inbound", StringComparison.OrdinalIgnoreCase))
                inbound.Add(MakeInputFile(path, headerRows: 2));
            else if (name.StartsWith("RSP_Outbound", StringComparison.OrdinalIgnoreCase))
                outbound.Add(MakeInputFile(path, headerRows: 2));
        }
        return (inbound, outbound);
    }

    /// <summary>Scans Transaction Report folder.</summary>
    public static List<InputFile> ScanTxnReport(string dir)
    {
        var result = new List<InputFile>();
        if (!Directory.Exists(dir)) return result;

        foreach (var path in Directory.GetFiles(dir))
        {
            string name = Path.GetFileName(path);
            if (name.StartsWith("~$")) continue;
            if (name.StartsWith("Transaction-Report", StringComparison.OrdinalIgnoreCase))
                result.Add(MakeInputFile(path, headerRows: 3));
        }
        return result;
    }

    private static InputFile MakeInputFile(string path, int headerRows)
    {
        var month = ParseMonthFromName(Path.GetFileName(path));
        int rows = CountDataRows(path, headerRows);
        return new InputFile(path, month, rows);
    }
}
