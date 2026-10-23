using OfficeOpenXml;

namespace BotReport2026.Readers;

public class SanctionData
{
    public HashSet<string> UnSanctionIds { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, HashSet<string>> UnSanctionNameToIds { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> ThSanctionIds { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public static class SanctionListReader
{
    /// <summary>
    /// Reads sanction lists from the Excel file.
    /// Both sheets: row1=title, row2=headers, data from row 3.
    /// UN: cl5=FIRST_NAME, cl10=NUMBER
    /// TH: cl10=IDENTITY_ID
    /// </summary>
    public static SanctionData Read(string filePath, Action<string>? log = null)
    {
        var result = new SanctionData();
        using var pkg = new ExcelPackage(new FileInfo(filePath));

        // UN Sanction List
        var wsUN = pkg.Workbook.Worksheets.FirstOrDefault(ws =>
            ws.Name.Contains("UN", StringComparison.OrdinalIgnoreCase));
        if (wsUN != null)
        {
            int totalRows = wsUN.Dimension?.Rows ?? 0;
            log?.Invoke($"[UN Sanction] กำลังโหลด {totalRows - 2:N0} รายการ...");
            int count = 0;
            for (int row = 3; row <= totalRows; row++)
            {
                string name = wsUN.Cells[row, 5].Text?.Trim() ?? "";
                string id = wsUN.Cells[row, 10].Text?.Trim() ?? "";

                if (!string.IsNullOrWhiteSpace(id))
                {
                    result.UnSanctionIds.Add(id);
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        if (!result.UnSanctionNameToIds.TryGetValue(id, out var nameSet))
                        {
                            nameSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                            result.UnSanctionNameToIds[id] = nameSet;
                        }
                        // Names may have semicolons separating aliases
                        foreach (var alias in name.Split(';'))
                            nameSet.Add(alias.Trim());
                    }
                }
                count++;
                if (count % 100000 == 0)
                    log?.Invoke($"[UN Sanction] โหลดแล้ว {count:N0} รายการ...");
            }
            log?.Invoke($"[UN Sanction] โหลดเสร็จ {result.UnSanctionIds.Count:N0} รายการ ID");
        }

        // TH Sanction List (CFR)
        var wsTH = pkg.Workbook.Worksheets.FirstOrDefault(ws =>
            ws.Name.Contains("TH", StringComparison.OrdinalIgnoreCase));
        if (wsTH != null)
        {
            int totalRows = wsTH.Dimension?.Rows ?? 0;
            log?.Invoke($"[TH Sanction] กำลังโหลด {totalRows - 2:N0} รายการ...");
            int count = 0;
            for (int row = 3; row <= totalRows; row++)
            {
                string id = wsTH.Cells[row, 10].Text?.Trim() ?? "";
                if (!string.IsNullOrWhiteSpace(id))
                    result.ThSanctionIds.Add(id);

                count++;
                if (count % 100000 == 0)
                    log?.Invoke($"[TH Sanction] โหลดแล้ว {count:N0} รายการ...");
            }
            log?.Invoke($"[TH Sanction] โหลดเสร็จ {result.ThSanctionIds.Count:N0} รายการ ID");
        }

        return result;
    }
}
