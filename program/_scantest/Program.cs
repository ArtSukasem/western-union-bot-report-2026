using BotReport2026.Readers;
using OfficeOpenXml;

ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\.."));
string rspDir = Path.Combine(root, "input-rsp");
string txnDir = Path.Combine(root, "input-transaction-report");

Console.WriteLine($"RSP dir: {rspDir}");
var (ib, ob) = InputFolderScanner.ScanRsp(rspDir);
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
