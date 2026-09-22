using OfficeOpenXml;

namespace BotReport2026.Readers;

/// <summary>Branch address parts for DS_SAE field 15 (สถานที่ทำธุรกรรม).</summary>
public record BranchAddress(string Account, string[] Parts)
{
    /// <summary>Cols B–H joined with spaces, per the v3 remark for SAE field 15.</summary>
    public string Combined => string.Join(" ",
        Parts.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p.Trim()));
}

/// <summary>
/// The four lookup tables extracted from the requirements v3 workbook into SAE-lookups/
/// by tools/extract_v3_lookups.py.
/// </summary>
public class ReferenceData
{
    /// <summary>Branch account (cl22 / cl16) → address. Keys are uppercase.</summary>
    public Dictionary<string, BranchAddress> BranchAddresses { get; set; } = new();

    /// <summary>Country name (uppercase) → ISO 3166-1 alpha-2.</summary>
    public Dictionary<string, string> CountryCodes { get; set; } = new();

    /// <summary>Rule code → คำอธิบายพฤติกรรม for DS_SBE field 5.</summary>
    public Dictionary<string, string> BehaviorDescriptions { get; set; } = new();

    /// <summary>Dropdown options for DS_SAE field 7, in sheet order.</summary>
    public List<string> EddReasons { get; set; } = new();

    /// <summary>
    /// Normalizes an RSP country value (cl77/cl42 issuer, cl66/cl31 nationality) to ISO2.
    /// The data mixes full names ("THAILAND") with codes ("TH"), so the lookup file wins
    /// first — it also corrects two-letter values that are not valid ISO2 — then any
    /// remaining two-letter value passes through. Unmapped values return "".
    /// </summary>
    public string ToIso2(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "";
        string norm = raw.Trim().ToUpperInvariant();
        if (CountryCodes.TryGetValue(norm, out var iso)) return iso;
        if (norm.Length == 2 && char.IsLetter(norm[0]) && char.IsLetter(norm[1])) return norm;
        return "";
    }

    /// <summary>DS_SAE field 4 — Thai nationality gets the Thai counterparty code.</summary>
    public string ResolveCounterpartyType(string nationality) =>
        ToIso2(nationality) == "TH"
            ? ClassificationTables.CounterpartyThai
            : ClassificationTables.CounterpartyForeign;
}

public static class ReferenceDataReader
{
    public const string AddressBranchFile = "address_branch.xlsx";
    public const string CountryCodeFile = "country_code.xlsx";
    public const string BehaviorDescriptionsFile = "behavior_descriptions.xlsx";
    public const string EddReasonsFile = "edd_reasons.xlsx";

    /// <summary>
    /// Reads all four lookup workbooks from <paramref name="lookupDir"/>.
    /// Each is single-header-row with data from row 2. A missing file is logged and
    /// leaves that table empty rather than failing the run.
    /// </summary>
    public static ReferenceData Read(string lookupDir, Action<string>? log = null)
    {
        var data = new ReferenceData();

        ForEachRow(lookupDir, AddressBranchFile, 8, log, cells =>
        {
            string account = cells[0];
            if (string.IsNullOrWhiteSpace(account)) return;
            data.BranchAddresses[account.ToUpperInvariant()] =
                new BranchAddress(account, cells.Skip(1).ToArray());
        });

        ForEachRow(lookupDir, CountryCodeFile, 2, log, cells =>
        {
            if (string.IsNullOrWhiteSpace(cells[0]) || string.IsNullOrWhiteSpace(cells[1])) return;
            data.CountryCodes[cells[0].ToUpperInvariant()] = cells[1].ToUpperInvariant();
        });

        ForEachRow(lookupDir, BehaviorDescriptionsFile, 2, log, cells =>
        {
            if (string.IsNullOrWhiteSpace(cells[0])) return;
            data.BehaviorDescriptions[cells[0]] = cells[1];
        });

        ForEachRow(lookupDir, EddReasonsFile, 2, log, cells =>
        {
            if (string.IsNullOrWhiteSpace(cells[0])) return;
            data.EddReasons.Add(cells[0]);
        });

        log?.Invoke(
            $"[Reference] สาขา {data.BranchAddresses.Count} แห่ง, ประเทศ {data.CountryCodes.Count} รายการ, " +
            $"คำอธิบายพฤติกรรม {data.BehaviorDescriptions.Count} รายการ, เหตุผล EDD {data.EddReasons.Count} รายการ");
        return data;
    }

    private static void ForEachRow(
        string dir, string fileName, int columns, Action<string>? log, Action<string[]> onRow)
    {
        string path = Path.Combine(dir, fileName);
        if (!File.Exists(path))
        {
            log?.Invoke($"[Reference] ไม่พบไฟล์: {fileName}");
            return;
        }

        using var pkg = new ExcelPackage(new FileInfo(path));
        var ws = pkg.Workbook.Worksheets[0];
        int rows = ws.Dimension?.Rows ?? 0;
        for (int row = 2; row <= rows; row++)
        {
            var cells = new string[columns];
            for (int c = 0; c < columns; c++)
                cells[c] = ws.Cells[row, c + 1].Text?.Trim() ?? "";
            onRow(cells);
        }
    }
}
