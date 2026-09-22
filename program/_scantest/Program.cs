using BotReport2026.Engine;
using BotReport2026.Models;
using BotReport2026.Readers;
using BotReport2026.Writers;
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

// ---------------------------------------------------------------------------
// Field check: how the v3 DS_SBE/DS_SAE derived fields resolve over the real data.
// A near-zero Thai count means country_code.xlsx is not being applied.
// ---------------------------------------------------------------------------
string lookupDir = Path.Combine(root, "SAE-lookups");
var reference = ReferenceDataReader.Read(lookupDir, m => Console.WriteLine($"   {m}"));

Console.WriteLine("\n===== FIELD CHECK (v3 derived fields) =====");

void FieldCheck(List<InputFile> files, string direction)
{
    var rows = files.SelectMany(f => RspReader.ReadFile(f.Path, direction)).ToList();
    if (rows.Count == 0) return;

    Console.WriteLine($"\n== {direction}: {rows.Count:N0} rows");

    void Tally(string label, Func<RspTransaction, string> project)
    {
        var groups = rows.GroupBy(project)
                         .OrderByDescending(g => g.Count())
                         .Select(g => $"{(string.IsNullOrEmpty(g.Key) ? "(blank)" : g.Key)}={g.Count():N0}");
        Console.WriteLine($"   {label,-22}: {string.Join(", ", groups.Take(6))}");
    }

    Tally("IdTypeCode", r => ClassificationTables.ResolveIdType(r.IdType));
    Tally("IssuerCountry(ISO2)", r => reference.ToIso2(r.IdIssuerCountry));
    Tally("CounterpartyType", r => reference.ResolveCounterpartyType(r.Nationality));
    Tally("OccupationCode", r => ClassificationTables.ResolveOccupation(r.Occupation));

    int unmappedCountry = rows.Count(r => !string.IsNullOrWhiteSpace(r.IdIssuerCountry) &&
                                          reference.ToIso2(r.IdIssuerCountry).Length == 0);
    int noAddress = rows.Count(r => string.IsNullOrWhiteSpace(r.FullAddress));
    int noPostal = rows.Count(r => string.IsNullOrWhiteSpace(r.PostalCode));
    int branchKnown = rows.Count(r => reference.BranchAddresses.ContainsKey(
        r.BranchAccountId.Trim().ToUpperInvariant()));

    Console.WriteLine($"   unmapped country      : {unmappedCountry:N0}");
    Console.WriteLine($"   blank address / postal: {noAddress:N0} / {noPostal:N0}");
    Console.WriteLine($"   branch found in lookup: {branchKnown:N0}");

    var s = rows[0];
    Console.WriteLine($"   sample address        : \"{s.FullAddress}\"");
}

FieldCheck(ib.Where(IsRep).ToList(), "IB");
FieldCheck(ob.Where(IsRep).ToList(), "OB");

// ---------------------------------------------------------------------------
// Full engine run against the detected reporting month, then CSV export.
// ---------------------------------------------------------------------------
if (args.Contains("--run"))
{
    Console.WriteLine("\n===== ENGINE RUN =====");
    var p = new RunParameters
    {
        ReportingMonthInboundFiles = ib.Where(IsRep).Select(f => f.Path).ToList(),
        ReportingMonthOutboundFiles = ob.Where(IsRep).Select(f => f.Path).ToList(),
        HistoricalInboundFiles = ib.Where(f => !IsRep(f)).Select(f => f.Path).ToList(),
        HistoricalOutboundFiles = ob.Where(f => !IsRep(f)).Select(f => f.Path).ToList(),
        TransactionReportFiles = txn.Where(IsRep).Select(f => f.Path).ToList(),
        SanctionListPath = Path.Combine(root, "lookups", "saction_list.xlsx"),
        CrimeListPath = Path.Combine(root, "lookups",
            "financial_crime_individuals_suspicious_counter_customer_behavior.xlsx"),
        OccupationMapPath = Path.Combine(root, "mapper",
            "list_of_occupation_expected_monthly_income.xlsx"),
        ReferenceDataDirectory = lookupDir,
        ReportingMonth = reporting,
        Config = new AppConfig(),
        OutputDirectory = Path.Combine(root, "report-results")
    };

    var (sbePath, saePath) = new ReportEngine()
        .RunAsync(p, Console.WriteLine, pr => Console.WriteLine($"   [{pr.Percent,3}%] {pr.Stage}"))
        .GetAwaiter().GetResult();
    Console.WriteLine($"Output: {Path.GetFileName(sbePath)}, {Path.GetFileName(saePath)}");
    // DS_SAE's .csv is produced by the button inside the workbook, not from here.

    // Round-trip the macro-enabled workbook: confirm the VBA project survived the
    // save and that the button is still pointing at the entry point.
    using (var check = new ExcelPackage(new FileInfo(saePath)))
    {
        var vba = check.Workbook.VbaProject;
        Console.WriteLine($"VBA project modules: {(vba == null ? "(none)" : string.Join(", ", vba.Modules.Select(m => m.Name)))}");
        var mod = vba?.Modules[SaeVbaMacro.ModuleName];
        if (mod != null)
        {
            var code = mod.Code ?? "";
            Console.WriteLine($"  {SaeVbaMacro.ModuleName}: {code.Length} chars, " +
                              $"entry point present={code.Contains("Sub " + SaeVbaMacro.EntryPoint)}, " +
                              $"ascii-only={code.All(c => c < 128)}");
            if (args.Contains("--dumpvba")) Console.WriteLine(code);
        }
        var sheet = check.Workbook.Worksheets["DS_SAE"];
        Console.WriteLine($"  rows={sheet.Dimension.Rows}, cols={sheet.Dimension.Columns}, drawings={sheet.Drawings.Count}");
    }
}

// ---------------------------------------------------------------------------
// Explain one person: same walk-through the ตรวจสอบรายบุคคล tab shows.
//   dotnet run -- --explain 1479900333351
// ---------------------------------------------------------------------------
int ix = Array.IndexOf(args, "--explain");
if (ix >= 0 && ix + 1 < args.Length)
{
    string who = args[ix + 1];
    Console.WriteLine($"===== EXPLAIN {who} =====");
    var ep = new RunParameters
    {
        ReportingMonthInboundFiles = ib.Where(IsRep).Select(f => f.Path).ToList(),
        ReportingMonthOutboundFiles = ob.Where(IsRep).Select(f => f.Path).ToList(),
        HistoricalInboundFiles = ib.Where(f => !IsRep(f)).Select(f => f.Path).ToList(),
        HistoricalOutboundFiles = ob.Where(f => !IsRep(f)).Select(f => f.Path).ToList(),
        TransactionReportFiles = txn.Where(IsRep).Select(f => f.Path).ToList(),
        SanctionListPath = Path.Combine(root, "lookups", "saction_list.xlsx"),
        CrimeListPath = Path.Combine(root, "lookups",
            "financial_crime_individuals_suspicious_counter_customer_behavior.xlsx"),
        OccupationMapPath = Path.Combine(root, "mapper",
            "list_of_occupation_expected_monthly_income.xlsx"),
        ReferenceDataDirectory = lookupDir,
        ReportingMonth = reporting,
        Config = new AppConfig(),
        OutputDirectory = Path.Combine(root, "report-results")
    };

    var engine = new ReportEngine();
    var ctx = engine.LoadContextAsync(ep, _ => { }).GetAwaiter().GetResult();
    foreach (var sec in PersonExplainer.Explain(ctx, who))
    {
        Console.WriteLine($"[{sec.Verdict}] {sec.Title}");
        foreach (var line in sec.Lines) Console.WriteLine(line);
        Console.WriteLine();
    }
}
