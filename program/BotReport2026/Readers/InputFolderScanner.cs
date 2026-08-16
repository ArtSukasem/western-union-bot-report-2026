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
    public static int CountExcelDataRows(string path, int headerRows)
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

    /// <summary>
    /// Scans the inbound and outbound RSP folders; returns (inbound, outbound) lists.
    /// Direction comes from the folder the file sits in — the file name is not inspected.
    /// </summary>
    public static (List<InputFile> inbound, List<InputFile> outbound) ScanRsp(
        string inboundDir, string outboundDir) =>
        (ScanRspFolder(inboundDir), ScanRspFolder(outboundDir));

    /// <summary>Returns every .csv in the folder as an InputFile (Excel lock files skipped).</summary>
    private static List<InputFile> ScanRspFolder(string dir)
    {
        var result = new List<InputFile>();
        if (!Directory.Exists(dir)) return result;

        foreach (var path in Directory.GetFiles(dir))
        {
            string name = Path.GetFileName(path);
            if (name.StartsWith("~$")) continue; // skip Excel temp locks
            if (!Path.GetExtension(path).Equals(".csv", StringComparison.OrdinalIgnoreCase)) continue;

            var month = ParseMonthFromName(name);
            // RSP CSV has a single header row
            result.Add(new InputFile(path, month, CsvUtil.CountDataRows(path, headerRows: 1)));
        }
        return result;
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
                result.Add(new InputFile(path, ParseMonthFromName(name),
                    CountExcelDataRows(path, headerRows: 3)));
        }
        return result;
    }
}
