using OfficeOpenXml;

namespace BotReport2026.Readers;

/// <summary>One row of the occupation lookup: English name → expected income and BoT code.</summary>
public record OccupationEntry(string Name, decimal? ExpectedIncome, string BotCode);

/// <summary>
/// Which lookup row an RSP transaction's occupation resolved to, and how — kept so the
/// log and the explain tab can show why a person got a given income / code.
/// </summary>
/// <param name="FromOtherColumn">False = exact match on cl89 / cl54; true = fuzzy match on cl114 / cl104.</param>
/// <param name="Score">Similarity 0..1 (1 for an exact match).</param>
public record OccupationMatch(OccupationEntry Entry, bool FromOtherColumn, string MatchedValue, double Score);

/// <summary>
/// Resolves an RSP occupation to a row of the occupation lookup, for Rule 203-Retail
/// (expected income) and DS_SBE field 16 (BoT code):
///   1. the dropdown value (cl89 IB / cl54 OB) matched exactly on the English name;
///   2. if that is blank, OTHERS or not in the lookup, the free-text value
///      (cl114 IB / cl104 OB) matched fuzzily, best score at or above the threshold;
///   3. otherwise no match — field 16 falls back to "อื่น ๆ" and Rule 203 skips the person.
/// </summary>
public class OccupationLookup
{
    private readonly List<(OccupationEntry entry, string key)> _entries = new();
    private readonly Dictionary<string, OccupationEntry> _exact = new(StringComparer.Ordinal);
    private readonly Dictionary<(string, string), OccupationMatch?> _cache = new();

    /// <summary>Minimum similarity (0..1) for a cl114 / cl104 fuzzy match.</summary>
    public double FuzzyThreshold { get; }

    public int Count => _entries.Count;

    public OccupationLookup(IEnumerable<OccupationEntry> entries, double fuzzyThreshold)
    {
        FuzzyThreshold = fuzzyThreshold;
        foreach (var e in entries)
        {
            string key = Normalize(e.Name);
            if (key.Length == 0) continue;
            _entries.Add((e, key));
            _exact.TryAdd(key, e);   // first row wins when a name repeats in another case
        }
    }

    public OccupationMatch? Resolve(string primary, string other)
    {
        var cacheKey = (primary ?? "", other ?? "");
        if (_cache.TryGetValue(cacheKey, out var cached)) return cached;

        OccupationMatch? match = null;
        string p = Normalize(primary);
        if (!IsOther(p) && _exact.TryGetValue(p, out var hit))
            match = new OccupationMatch(hit, false, hit.Name, 1.0);
        else
        {
            string o = Normalize(other);
            if (!IsOther(o))
            {
                OccupationEntry? best = null;
                double bestScore = 0;
                foreach (var (entry, key) in _entries)
                {
                    double score = Similarity(o, key);
                    if (score > bestScore) { bestScore = score; best = entry; }
                }
                if (best != null && bestScore >= FuzzyThreshold)
                    match = new OccupationMatch(best, true, best.Name, bestScore);
            }
        }

        _cache[cacheKey] = match;
        return match;
    }

    /// <summary>The RSP's "no specific occupation" values, which never count as a match.</summary>
    public static bool IsOther(string normalized) =>
        normalized.Length == 0 || normalized == "OTHER" || normalized == "OTHERS";

    /// <summary>Uppercase, trimmed, internal whitespace collapsed to single spaces.</summary>
    public static string Normalize(string? s) =>
        string.IsNullOrWhiteSpace(s)
            ? ""
            : string.Join(' ', s.Trim().ToUpperInvariant()
                .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    /// <summary>
    /// 1 − (Damerau-Levenshtein distance / longer length), so one typo or one swapped
    /// pair costs one edit: HOUSEWIFT→HOUSEWIFE 0.89, RETRIED→RETIRED 0.86.
    /// </summary>
    public static double Similarity(string a, string b)
    {
        if (a.Length == 0 && b.Length == 0) return 1;
        int max = Math.Max(a.Length, b.Length);
        return 1.0 - (double)Distance(a, b) / max;
    }

    // Optimal string alignment distance: insert, delete, substitute, adjacent transposition.
    private static int Distance(string a, string b)
    {
        var d = new int[a.Length + 1, b.Length + 1];
        for (int i = 0; i <= a.Length; i++) d[i, 0] = i;
        for (int j = 0; j <= b.Length; j++) d[0, j] = j;
        for (int i = 1; i <= a.Length; i++)
        {
            for (int j = 1; j <= b.Length; j++)
            {
                int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);
                if (i > 1 && j > 1 && a[i - 1] == b[j - 2] && a[i - 2] == b[j - 1])
                    d[i, j] = Math.Min(d[i, j], d[i - 2, j - 2] + 1);
            }
        }
        return d[a.Length, b.Length];
    }
}

public static class OccupationReader
{
    private const string IncomeHeader = "Expected Monthly Income";
    private const string BotCodeHeader = "BoT code";

    /// <summary>
    /// Reads the sheet whose name contains "Occupation". cl1 = English name; the income
    /// and BoT code columns are found by their header text ("Expected Monthly Income",
    /// "BoT code") in the first few rows, since some versions of the file carry an extra
    /// "cl1, cl2, …" row above the real headers. Falls back to cl6 / cl7 with data from
    /// row 2 when no header is found.
    /// Rows naming OTHERS and note rows written in Thai are skipped. An income that is
    /// not a single number (e.g. "0 – 5,000") is read as unknown.
    /// </summary>
    public static OccupationLookup Read(string filePath, double fuzzyThreshold)
    {
        var entries = new List<OccupationEntry>();
        using var pkg = new ExcelPackage(new FileInfo(filePath));
        var ws = pkg.Workbook.Worksheets.FirstOrDefault(w =>
            w.Name.Contains("Occupation", StringComparison.OrdinalIgnoreCase));
        if (ws == null) return new OccupationLookup(entries, fuzzyThreshold);

        int totalRows = ws.Dimension?.End.Row ?? 0;
        int totalCols = ws.Dimension?.End.Column ?? 0;
        int incomeCol = 6, botCol = 7, firstDataRow = 2;

        for (int row = 1; row <= Math.Min(5, totalRows); row++)
        {
            for (int col = 1; col <= totalCols; col++)
            {
                string text = ws.Cells[row, col].Text?.Trim() ?? "";
                if (text.Equals(IncomeHeader, StringComparison.OrdinalIgnoreCase))
                { incomeCol = col; firstDataRow = row + 1; }
                else if (text.Equals(BotCodeHeader, StringComparison.OrdinalIgnoreCase))
                { botCol = col; firstDataRow = row + 1; }
            }
        }

        for (int row = firstDataRow; row <= totalRows; row++)
        {
            string name = ws.Cells[row, 1].Text?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(name) || name.Any(IsThai)) continue;
            if (OccupationLookup.IsOther(OccupationLookup.Normalize(name))) continue;

            string incStr = (ws.Cells[row, incomeCol].Text ?? "").Trim().Replace(",", "");
            decimal? income = decimal.TryParse(incStr,
                System.Globalization.NumberStyles.Number,
                System.Globalization.CultureInfo.InvariantCulture, out var inc) ? inc : null;

            string botCode = ws.Cells[row, botCol].Text?.Trim() ?? "";
            entries.Add(new OccupationEntry(name, income, botCode));
        }
        return new OccupationLookup(entries, fuzzyThreshold);
    }

    private static bool IsThai(char c) => c >= '฀' && c <= '๿';
}
