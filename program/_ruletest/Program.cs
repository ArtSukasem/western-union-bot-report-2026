using BotReport2026.Readers;
using OfficeOpenXml;

ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\.."));
string path = Path.Combine(root, "lookups", "financial_crime_individuals_suspicious_counter_customer_behavior.xlsx");

Console.WriteLine($"File exists: {File.Exists(path)}");
var data = CrimeSuspiciousReader.Read(path, Console.WriteLine);

Console.WriteLine("\n== บุคคลอาชญากรรมทางการเงิน (Rule 212) ==");
foreach (var c in data.CrimePersons)
    Console.WriteLine($"   '{c.FirstName}' '{c.LastName}'  → key='{c.FullNameKey}'");

Console.WriteLine("\n== พฤติกรรมหน้าร้านน่าสงสัย (Rule 301) ==");
foreach (var s in data.SuspiciousRetail)
    Console.WriteLine($"   MTCN='{s.MTCN}' ID='{s.IdNumber}' name='{s.FullNameKey}'");
