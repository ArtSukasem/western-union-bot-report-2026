using OfficeOpenXml;

namespace BotReport2026.Readers;

public record CrimePerson(string FirstName, string LastName)
{
    public string FullNameKey => $"{FirstName} {LastName}".Trim().ToUpperInvariant();
}

public record SuspiciousRetail(string MTCN, string FirstName, string LastName, string IdNumber)
{
    public string FullNameKey => $"{FirstName} {LastName}".Trim().ToUpperInvariant();
}

public class CrimeSuspiciousData
{
    public List<CrimePerson> CrimePersons { get; set; } = new();
    public List<SuspiciousRetail> SuspiciousRetail { get; set; } = new();
}

public static class CrimeSuspiciousReader
{
    /// <summary>
    /// Reads the crime/suspicious lookup file (2 sheets):
    ///  - "บุคคลอาชญากรรมทางการเงิน": Firstname, Lastname → Rule 212
    ///  - "พฤติกรรมลูกค้าหน้าร้านน่าสงสัย": MTCN, Firstname, Lastname, IDnumber → Rule 301
    /// Single header row; data from row 2.
    /// </summary>
    public static CrimeSuspiciousData Read(string filePath, Action<string>? log = null)
    {
        var result = new CrimeSuspiciousData();
        if (!File.Exists(filePath))
        {
            log?.Invoke($"[Crime/Suspicious] ไม่พบไฟล์: {Path.GetFileName(filePath)}");
            return result;
        }

        using var pkg = new ExcelPackage(new FileInfo(filePath));

        // Sheet: บุคคลอาชญากรรมทางการเงิน (financial crime persons)
        var wsCrime = pkg.Workbook.Worksheets.FirstOrDefault(w =>
            w.Name.Contains("อาชญากรรม", StringComparison.Ordinal));
        if (wsCrime != null)
        {
            int rows = wsCrime.Dimension?.Rows ?? 0;
            for (int row = 2; row <= rows; row++)
            {
                string first = wsCrime.Cells[row, 1].Text?.Trim() ?? "";
                string last = wsCrime.Cells[row, 2].Text?.Trim() ?? "";
                if (string.IsNullOrWhiteSpace(first) && string.IsNullOrWhiteSpace(last)) continue;
                result.CrimePersons.Add(new CrimePerson(first, last));
            }
        }

        // Sheet: พฤติกรรมลูกค้าหน้าร้านน่าสงสัย (suspicious storefront customers)
        var wsSusp = pkg.Workbook.Worksheets.FirstOrDefault(w =>
            w.Name.Contains("พฤติกรรม", StringComparison.Ordinal) ||
            w.Name.Contains("หน้าร้าน", StringComparison.Ordinal));
        if (wsSusp != null)
        {
            int rows = wsSusp.Dimension?.Rows ?? 0;
            for (int row = 2; row <= rows; row++)
            {
                string mtcn = wsSusp.Cells[row, 1].Text?.Trim() ?? "";
                string first = wsSusp.Cells[row, 2].Text?.Trim() ?? "";
                string last = wsSusp.Cells[row, 3].Text?.Trim() ?? "";
                string id = wsSusp.Cells[row, 4].Text?.Trim() ?? "";
                if (string.IsNullOrWhiteSpace(mtcn) && string.IsNullOrWhiteSpace(id) &&
                    string.IsNullOrWhiteSpace(first) && string.IsNullOrWhiteSpace(last)) continue;
                result.SuspiciousRetail.Add(new SuspiciousRetail(mtcn, first, last, id));
            }
        }

        log?.Invoke($"[Crime/Suspicious] บุคคลอาชญากรรม {result.CrimePersons.Count} ราย, พฤติกรรมหน้าร้าน {result.SuspiciousRetail.Count} ราย");
        return result;
    }
}
