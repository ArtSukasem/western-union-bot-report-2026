using BotReport2026.Models;
using BotReport2026.Readers;
using BotReport2026.Rules;
using BotReport2026.Writers;

namespace BotReport2026.Engine;

/// <summary>How far the run has got (0-100) and the stage it is on right now.</summary>
public readonly record struct RunProgress(int Percent, string Stage);

public class ReportEngine
{
    /// <summary>Inputs from the most recent Run/LoadContext, reused by the explain tab.</summary>
    public RuleContext? LastContext { get; private set; }

    /// <summary>DS_SBE rows the most recent Run produced, for cross-checking an explanation.</summary>
    public List<SbeRecord> LastSbeRecords { get; private set; } = new();

    /// <summary>
    /// Drops the cached inputs and results — called when the working folder changes, since
    /// everything loaded came from the folder the app has just stopped using.
    /// </summary>
    public void ResetContext()
    {
        LastContext = null;
        LastSbeRecords = new();
    }

    // Weight of each stage on the 0-100 bar. Loading RSP dominates the wall clock —
    // historical is a dozen files of ~20k rows — so it takes the largest slices.
    private const int WeightOccupationMap = 2;
    private const int WeightSanctionLists = 10;
    private const int WeightCrimeLists = 3;
    private const int WeightReferenceData = 3;
    private const int WeightReportingRsp = 12;
    private const int WeightHistoricalRsp = 30;
    private const int WeightTxnReport = 5;
    private const int WeightRules = 25;
    private const int WeightConsolidate = 3;
    private const int WeightWrite = 7;

    private readonly List<IRuleEngine> _rules = new()
    {
        new Rule101SanctionMatch(),
        new Rule202AbnormalVolume(),
        new Rule203IncomeMismatch(),
        new Rule206HighRiskBranch(),
        new Rule208Structuring(),
        new Rule209ContinuousTrading(),
        new Rule212FinancialCrime(),
        new Rule301SuspiciousRetail()
    };

    public async Task<(string sbePath, string saePath)> RunAsync(
        RunParameters p,
        Action<string> log,
        Action<RunProgress>? onProgress = null,
        CancellationToken ct = default)
    {
        return await Task.Run(() => Run(p, log, onProgress, ct), ct);
    }

    /// <summary>
    /// Loads every input the rules read — lookups plus RSP plus Transaction Report — and
    /// stops there. The explain tab needs the same data as a run but produces no files.
    /// </summary>
    public async Task<RuleContext> LoadContextAsync(
        RunParameters p,
        Action<string> log,
        Action<RunProgress>? onProgress = null,
        CancellationToken ct = default)
    {
        return await Task.Run(() =>
        {
            var progress = new ProgressTracker(onProgress);
            void Log(string msg) => log($"[{DateTime.Now:HH:mm:ss}] {msg}");
            var ctx = BuildContext(p, Log, progress, ct);
            LastContext = ctx;
            // BuildContext only covers the loading slices, so finish the bar explicitly.
            onProgress?.Invoke(new RunProgress(100, "โหลดข้อมูลเสร็จ"));
            return ctx;
        }, ct);
    }

    private (string sbePath, string saePath) Run(
        RunParameters p, Action<string> log, Action<RunProgress>? onProgress, CancellationToken ct)
    {
        var progress = new ProgressTracker(onProgress);
        void Log(string msg) => log($"[{DateTime.Now:HH:mm:ss}] {msg}");

        var ctx = BuildContext(p, Log, progress, ct);
        LastContext = ctx;
        var referenceData = ctx.ReferenceData;

        // 7. Run all rules
        var ruleHits = new List<SbeRecord>();

        progress.Begin($"กำลังรัน Rule (0/{_rules.Count})", WeightRules);
        for (int i = 0; i < _rules.Count; i++)
        {
            var rule = _rules[i];
            ct.ThrowIfCancellationRequested();
            progress.Within((double)i / _rules.Count,
                $"กำลังรัน Rule {rule.RuleCode} ({i + 1}/{_rules.Count})");
            Log($"กำลังรัน Rule {rule.RuleCode}...");
            var (sbe, _) = rule.Execute(ctx);
            ruleHits.AddRange(sbe);
        }

        // 8. One row per MTCN — DS_SBE's key is รหัสสถาบัน + งวดข้อมูล + เลขที่อ้างอิง,
        //    so a transaction flagged by several rules is reported under the first rule
        //    in _rules order. Rules run in that order, so the first hit already wins.
        progress.Begin("กำลังรวมผลและตรวจสอบข้อมูล...", WeightConsolidate);
        var allSbe = Deduplicate(ruleHits, Log);

        // 9. Every DS_SBE row gets a DS_SAE row keyed on the same MTCN
        var allSae = allSbe.Select(s => SaeRecordFactory.From(s, ctx)).ToList();

        // 10. Citizen-ID validation (DS_SBE field 8)
        var errors = ValidateCitizenIds(allSbe);

        LastSbeRecords = allSbe;
        Log($"รวม DS_SBE: {allSbe.Count} รายการ, DS_SAE: {allSae.Count} รายการ");

        // 11. Write outputs
        string ymCode = p.ReportingMonth.ToString("yyyyMM");
        Directory.CreateDirectory(p.OutputDirectory);
        // DS_SBE is fully derived, so it goes straight out as the submission .csv.
        // DS_SAE needs staff EDD input first, so it is a macro-enabled workbook that
        // exports its own .csv from a button inside Excel.
        string sbePath = Path.Combine(p.OutputDirectory, $"DS_SBE_{ymCode}.csv");
        string saePath = Path.Combine(p.OutputDirectory, $"DS_SAE_{ymCode}.xlsm");
        string errorPath = Path.Combine(p.OutputDirectory, "error.xlsx");

        progress.Begin($"กำลังเขียน {Path.GetFileName(sbePath)}...", WeightWrite);
        Log($"กำลังเขียน {Path.GetFileName(sbePath)}...");
        SbeWriter.Write(allSbe, sbePath);

        progress.Within(0.5, $"กำลังเขียน {Path.GetFileName(saePath)}...");
        Log($"กำลังเขียน {Path.GetFileName(saePath)}...");
        SaeWriter.Write(allSae, saePath, referenceData);

        if (ErrorWriter.Write(errors, errorPath))
            Log($"⚠ พบเลขบัตรประชาชนไม่ถูกต้อง {errors.Count} รายการ — บันทึกที่ {Path.GetFileName(errorPath)}");

        progress.Finish("✅ เสร็จสิ้น");
        Log($"✅ เสร็จสิ้น! ไฟล์บันทึกที่: {p.OutputDirectory}");
        return (sbePath, saePath);
    }

    /// <summary>
    /// Reads every input file and assembles the <see cref="RuleContext"/> the rules run on.
    /// Shared by <see cref="Run"/> and <see cref="LoadContextAsync"/>.
    /// </summary>
    private RuleContext BuildContext(
        RunParameters p, Action<string> Log, ProgressTracker progress, CancellationToken ct)
    {
        // Every stage announces itself once: same text to the log and to the progress label.
        void Stage(string msg, int weight) { progress.Begin(msg, weight); Log(msg); }

        // 1. Occupation map
        Stage("กำลังโหลด Occupation Map...", WeightOccupationMap);
        var occMap = OccupationReader.ReadOccupationMap(p.OccupationMapPath);
        Log($"โหลด Occupation Map เสร็จ {occMap.Count} รายการ");
        ct.ThrowIfCancellationRequested();

        // 2. Sanction lists
        Stage("กำลังโหลด Sanction Lists (อาจใช้เวลา 30-60 วินาที)...", WeightSanctionLists);
        var sanctionData = SanctionListReader.Read(p.SanctionListPath, Log);
        ct.ThrowIfCancellationRequested();

        // 2b. Crime / suspicious-retail lists (Rule 212 & 301)
        Stage("กำลังโหลดรายชื่ออาชญากรรม/พฤติกรรมหน้าร้านน่าสงสัย...", WeightCrimeLists);
        var crimeData = CrimeSuspiciousReader.Read(p.CrimeListPath, Log);
        ct.ThrowIfCancellationRequested();

        // 2c. DS_SBE/DS_SAE reference tables (branch addresses, country codes,
        //     behaviour descriptions, EDD reasons)
        Stage("กำลังโหลดตารางอ้างอิง DS_SBE/DS_SAE...", WeightReferenceData);
        var referenceData = ReferenceDataReader.Read(p.ReferenceDataDirectory, Log);
        ct.ThrowIfCancellationRequested();

        // 3. Reporting month RSP
        Stage("กำลังโหลด RSP เดือนที่รายงาน...", WeightReportingRsp);
        var reportingRsp = LoadRsp(p.ReportingMonthInboundFiles, p.ReportingMonthOutboundFiles,
            p.Config, Log, f => progress.Within(f));
        Log($"โหลด RSP รายงาน: {reportingRsp.Count:N0} รายการ");
        ct.ThrowIfCancellationRequested();

        // 4. Historical RSP
        Stage("กำลังโหลด RSP ย้อนหลัง...", WeightHistoricalRsp);
        var historicalRsp = LoadRsp(p.HistoricalInboundFiles, p.HistoricalOutboundFiles,
            p.Config, Log, f => progress.Within(f));
        Log($"โหลด RSP ย้อนหลัง: {historicalRsp.Count:N0} รายการ");
        ct.ThrowIfCancellationRequested();

        // 5. Transaction report
        Stage("กำลังโหลด Transaction Report...", WeightTxnReport);
        var txnReport = new List<TransactionReportRow>();
        for (int i = 0; i < p.TransactionReportFiles.Count; i++)
        {
            var f = p.TransactionReportFiles[i];
            var rows = TransactionReportReader.ReadFile(f);
            txnReport.AddRange(rows);
            progress.Within((i + 1.0) / p.TransactionReportFiles.Count);
            Log($"  {Path.GetFileName(f)}: {rows.Count} รายการที่เข้าเกณฑ์");
        }
        ct.ThrowIfCancellationRequested();

        // 6. Build context
        return new RuleContext
        {
            ReportingMonthRsp = reportingRsp,
            HistoricalRsp = historicalRsp,
            TransactionReport = txnReport,
            SanctionData = sanctionData,
            CrimeSuspicious = crimeData,
            OccupationMap = occMap,
            ReferenceData = referenceData,
            Config = p.Config,
            ReportingMonth = new DateTime(p.ReportingMonth.Year, p.ReportingMonth.Month, 1),
            ReportDate = DateTime.Today.ToString("yyyy-MM-dd"),
            LogCallback = Log
        };
    }

    /// <summary>
    /// Keeps the first record for each MTCN and logs the rules that lost, so a
    /// transaction dropped from a rule's output is visible rather than silent.
    /// </summary>
    private static List<SbeRecord> Deduplicate(List<SbeRecord> records, Action<string> log)
    {
        var kept = new Dictionary<string, SbeRecord>(StringComparer.OrdinalIgnoreCase);
        int dropped = 0;

        foreach (var r in records)
        {
            if (string.IsNullOrWhiteSpace(r.ReferenceNo)) continue;
            if (kept.TryGetValue(r.ReferenceNo, out var existing))
            {
                if (existing.RuleCode != r.RuleCode)
                {
                    log($"  MTCN {r.ReferenceNo}: เข้าเกณฑ์ทั้ง {existing.RuleCode} และ {r.RuleCode} " +
                        $"— รายงานเป็น {existing.BehaviorType}");
                }
                dropped++;
                continue;
            }
            kept[r.ReferenceNo] = r;
        }

        if (dropped > 0)
            log($"รวมรายการซ้ำ {dropped} รายการเป็นรายการเดียวต่อ MTCN");

        return kept.Values.ToList();
    }

    /// <summary>
    /// DS_SBE field 8: a Thai citizen ID must be exactly 13 digits. Rows that fail are
    /// still reported — the invalid ID is only recorded as a warning in error.xlsx.
    /// </summary>
    private static List<ReportError> ValidateCitizenIds(List<SbeRecord> records)
    {
        var errors = new List<ReportError>();
        foreach (var r in records)
        {
            if (r.IdTypeCode != ClassificationTables.IdTypePersonalId) continue;

            string id = r.PersonRefId.Trim();
            if (id.Length == 13 && id.All(char.IsDigit)) continue;

            errors.Add(new ReportError(
                r.ReferenceNo, id, ErrorWriter.InvalidCitizenId,
                r.Source?.SourceFile ?? "", r.Source?.SourceRow ?? 0));
        }
        return errors;
    }

    /// <param name="onProgress">Fraction of the file list read so far (0..1), for the progress bar.</param>
    private static List<RspTransaction> LoadRsp(
        List<string> ibFiles, List<string> obFiles, AppConfig config, Action<string> log,
        Action<double>? onProgress = null)
    {
        var statuses = config.RspIncludedStatuses ?? new List<string>();
        var result = new List<RspTransaction>();
        int totalFiles = ibFiles.Count + obFiles.Count;
        int filesDone = 0;

        void LoadInto(IEnumerable<string> files, string direction)
        {
            foreach (var f in files)
            {
                var read = RspReader.Read(f, direction, statuses);
                result.AddRange(read.Rows);
                string skipped = read.SkippedByStatus > 0
                    ? $" (ข้าม {read.SkippedByStatus:N0} รายการ สถานะไม่อยู่ใน {string.Join("/", statuses)})"
                    : "";
                log($"  {direction} {Path.GetFileName(f)}: {read.Rows.Count:N0} รายการ{skipped}");
                filesDone++;
                if (totalFiles > 0) onProgress?.Invoke((double)filesDone / totalFiles);
            }
        }

        LoadInto(ibFiles, "IB");
        LoadInto(obFiles, "OB");
        return result;
    }

    /// <summary>
    /// Turns the fixed stage weights into a 0-100 percentage. Each stage claims a slice of the
    /// bar; a slow stage reports fractional progress inside its own slice, so the bar keeps
    /// moving while a dozen RSP files are read instead of jumping once at the end.
    /// </summary>
    private sealed class ProgressTracker
    {
        private readonly Action<RunProgress>? _sink;
        private int _completed;   // weight of the stages already closed
        private int _weight;      // weight of the stage running now
        private string _stage = "";

        public ProgressTracker(Action<RunProgress>? sink) => _sink = sink;

        /// <summary>Closes the current stage and opens one worth <paramref name="weight"/> points.</summary>
        public void Begin(string stage, int weight)
        {
            _completed += _weight;
            _weight = weight;
            _stage = stage;
            Emit(0);
        }

        /// <summary>Reports progress inside the current stage, 0..1.</summary>
        public void Within(double fraction, string? stage = null)
        {
            if (stage != null) _stage = stage;
            Emit(fraction);
        }

        /// <summary>Closes the last stage and pins the bar at 100%.</summary>
        public void Finish(string stage)
        {
            _completed += _weight;
            _weight = 0;
            _stage = stage;
            Emit(0);
        }

        private void Emit(double fraction)
        {
            double pct = _completed + _weight * Math.Clamp(fraction, 0, 1);
            _sink?.Invoke(new RunProgress((int)Math.Clamp(Math.Round(pct), 0, 100), _stage));
        }
    }
}
