using BotReport2026.Readers;
using OfficeOpenXml;

ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\.."));
string rspIbDir = Path.Combine(root, "input-rsp-inbound");
string rspObDir = Path.Combine(root, "input-rsp-outbound");
string txnDir = Path.Combine(root, "input-transaction-report");

Console.WriteLine($"RSP inbound dir : {rspIbDir}");
Console.WriteLine($"RSP outbound dir: {rspObDir}");
var (ib, ob) = InputFolderScanner.ScanRsp(rspIbDir, rspObDir);
var txn = InputFolderScanner.ScanTxnReport(txnDir);

var months = ib.Concat(ob).Concat(txn).Where(f => f.Month.HasValue).Select(f => f.Month!.Value).ToList();
var reporting = months.Count > 0 ? months.Max() : DateTime.Today;
Console.WriteLine($"\nDetected reporting month: {reporting:MMM yyyy}\n");

void Dump(string label, List<InputFile> files)
{
    Console.WriteLine($"== {label} ==");
    foreach (var f in files) Console.WriteLine($"   {f.Display}");
}

Dump("Inbound (all)", ib);
Dump("Outbound (all)", ob);
Dump("TxnReport (all)", txn);

bool IsRep(InputFile f) => f.Month.HasValue && f.Month.Value.Year == reporting.Year && f.Month.Value.Month == reporting.Month;
Console.WriteLine($"\n-- Reporting IB: {string.Join(", ", ib.Where(IsRep).Select(f => f.FileName))}");
Console.WriteLine($"-- Historical IB: {string.Join(", ", ib.Where(f => !IsRep(f)).Select(f => f.FileName))}");
Console.WriteLine($"-- Reporting Txn: {string.Join(", ", txn.Where(IsRep).Select(f => f.FileName))}");

// Read-back check: parse each RSP CSV and report what came out
void ReadCheck(List<InputFile> files, string direction)
{
    foreach (var f in files)
    {
        var res = RspReader.Read(f.Path, direction);
        Console.WriteLine($"\n== {direction} {f.FileName}");
        Console.WriteLine($"   kept {res.Rows.Count:N0} / skipped-by-status {res.SkippedByStatus:N0}");
        if (res.Rows.Count == 0) continue;

        var months = res.Rows.Select(r => r.TransactionDate?.ToString("yyyy-MM") ?? "(no date)")
                             .GroupBy(m => m).OrderBy(g => g.Key)
                             .Select(g => $"{g.Key}={g.Count():N0}");
        Console.WriteLine($"   date months : {string.Join(", ", months)}");
        Console.WriteLine($"   no PersonId : {res.Rows.Count(r => string.IsNullOrWhiteSpace(r.PersonId)):N0}");
        Console.WriteLine($"   total amount: {res.Rows.Sum(r => r.Principal):N2}");

        var r0 = res.Rows[0];
        Console.WriteLine($"   first row   : MTCN={r0.MTCN} date={r0.TransactionDate:yyyy-MM-dd} " +
                          $"time={r0.TransactionTime} amt={r0.Principal} branch={r0.BranchAccountId} " +
                          $"name=\"{r0.FullName}\" id={r0.PersonId} occ=\"{r0.Occupation}\"");
    }
}

ReadCheck(ib.Where(IsRep).ToList(), "IB");
ReadCheck(ob.Where(IsRep).ToList(), "OB");
