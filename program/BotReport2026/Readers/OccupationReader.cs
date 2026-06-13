using OfficeOpenXml;

namespace BotReport2026.Readers;

public static class OccupationReader
{
    // Hardcoded fallbacks for known RSP→Mapper mismatches
    private static readonly Dictionary<string, string> FallbackMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["HOUSEWIFE"] = "Housewife/Child Care",
        ["NON-OFFICE EMPLOYEE"] = "Non-Office Employee",
        ["COMPANY EMPLOYEE"] = "Office Professional",
        ["BUSINESS OWNER"] = "Retail Sales",
        ["FREELANCE"] = "Professional Service Practitioner",
        ["DOMESTIC HELPER"] = "Domestic Helper",
        ["DRIVER"] = "Driver",
        ["RETIRED"] = "Retired",
    };

    /// <summary>
    /// Returns Dictionary mapping occupation name (uppercase) → expected monthly income.
    /// Single header row (row1 = field names); data from row2.
    /// cl1=OccupationName(EN), cl6=ExpectedMonthlyIncome
    /// </summary>
    public static Dictionary<string, decimal> ReadOccupationMap(string filePath)
    {
        var result = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        using var pkg = new ExcelPackage(new FileInfo(filePath));
        var ws = pkg.Workbook.Worksheets.FirstOrDefault(w =>
            w.Name.Contains("Occupation", StringComparison.OrdinalIgnoreCase));
        if (ws == null) return result;

        int totalRows = ws.Dimension?.Rows ?? 0;
        for (int row = 2; row <= totalRows; row++)
        {
            string name = ws.Cells[row, 1].Text?.Trim() ?? "";
            string incStr = ws.Cells[row, 6].Text?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(name)) continue;
            if (decimal.TryParse(incStr.Replace(",", ""),
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var inc))
            {
                result[name] = inc;
            }
        }
        return result;
    }

    /// <summary>
    /// Looks up expected monthly income for a given occupation string from RSP.
    /// Returns null if no match found.
    /// </summary>
    public static decimal? FindExpectedIncome(string rawOccupation, Dictionary<string, decimal> map)
    {
        if (string.IsNullOrWhiteSpace(rawOccupation)) return null;
        string norm = rawOccupation.Trim();

        // 1. Exact match
        if (map.TryGetValue(norm, out var inc)) return inc;

        // 2. Check fallback map
        if (FallbackMap.TryGetValue(norm, out var mappedName) && map.TryGetValue(mappedName, out inc))
            return inc;

        // 3. Try case-insensitive partial match (RSP value contained in mapper key)
        string normUpper = norm.ToUpperInvariant();
        foreach (var kv in map)
        {
            if (kv.Key.ToUpperInvariant().Contains(normUpper) ||
                normUpper.Contains(kv.Key.ToUpperInvariant()))
                return kv.Value;
        }

        return null;
    }
}
