using BotReport2026.Models;
using BotReport2026.Readers;
using BotReport2026.Rules;
using BotReport2026.Writers;

namespace BotReport2026.Engine;

public class ReportEngine
{
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
        CancellationToken ct = default)
    {
        return await Task.Run(() => Run(p, log, ct), ct);
    }

    private (string sbePath, string saePath) Run(RunParameters p, Action<string> log, CancellationToken ct)
    {
        void Log(string msg) => log($"[{DateTime.Now:HH:mm:ss}] {msg}");

        // 1. Occupation map
        Log("กำลังโหลด Occupation Map...");
        var occMap = OccupationReader.ReadOccupationMap(p.OccupationMapPath);
        Log($"โหลด Occupation Map เสร็จ {occMap.Count} รายการ");
        ct.ThrowIfCancellationRequested();

        // 2. Sanction lists
        Log("กำลังโหลด Sanction Lists (อาจใช้เวลา 30-60 วินาที)...");
        var sanctionData = SanctionListReader.Read(p.SanctionListPath, Log);
        ct.ThrowIfCancellationRequested();

        // 2b. Crime / suspicious-retail lists (Rule 212 & 301)
        Log("กำลังโหลดรายชื่ออาชญากรรม/พฤติกรรมหน้าร้านน่าสงสัย...");
        var crimeData = CrimeSuspiciousReader.Read(p.CrimeListPath, Log);
        ct.ThrowIfCancellationRequested();

        // 3. Reporting month RSP
        Log("กำลังโหลด RSP เดือนที่รายงาน...");
        var reportingRsp = LoadRsp(p.ReportingMonthInboundFiles, p.ReportingMonthOutboundFiles, Log);
        Log($"โหลด RSP รายงาน: {reportingRsp.Count:N0} รายการ");
        ct.ThrowIfCancellationRequested();

        // 4. Historical RSP
        Log("กำลังโหลด RSP ย้อนหลัง...");
        var historicalRsp = LoadRsp(p.HistoricalInboundFiles, p.HistoricalOutboundFiles, Log);
        Log($"โหลด RSP ย้อนหลัง: {historicalRsp.Count:N0} รายการ");
        ct.ThrowIfCancellationRequested();

        // 5. Transaction report
        Log("กำลังโหลด Transaction Report...");
        var txnReport = new List<TransactionReportRow>();
        foreach (var f in p.TransactionReportFiles)
        {
            var rows = TransactionReportReader.ReadFile(f);
            txnReport.AddRange(rows);
            Log($"  {Path.GetFileName(f)}: {rows.Count} รายการที่เข้าเกณฑ์");
        }
        ct.ThrowIfCancellationRequested();

        // 6. Build context
        var ctx = new RuleContext
        {
            ReportingMonthRsp = reportingRsp,
            HistoricalRsp = historicalRsp,
            TransactionReport = txnReport,
            SanctionData = sanctionData,
            CrimeSuspicious = crimeData,
            OccupationMap = occMap,
            Config = p.Config,
            ReportingMonth = new DateTime(p.ReportingMonth.Year, p.ReportingMonth.Month, 1),
            LogCallback = Log
        };

        // 7. Run all rules
        var allSbe = new List<SbeRecord>();
        var allSae = new List<SaeRecord>();

        foreach (var rule in _rules)
        {
            ct.ThrowIfCancellationRequested();
            Log($"กำลังรัน Rule {rule.RuleCode}...");
            var (sbe, sae) = rule.Execute(ctx);
            allSbe.AddRange(sbe);
            allSae.AddRange(sae);
        }

        // 8. Assign sequential reference numbers
        string ymCode = p.ReportingMonth.ToString("yyyyMM");
        for (int i = 0; i < allSbe.Count; i++)
            allSbe[i].ReferenceNo = $"SBE{ymCode}{(i + 1):D4}";

        // 9. Link SAE records to SBE reference numbers (Rule 203 Online)
        int saeIdx = 0;
        for (int i = 0; i < allSbe.Count && saeIdx < allSae.Count; i++)
        {
            if (allSbe[i].HasSae)
            {
                allSae[saeIdx].ReferenceNo = allSbe[i].ReferenceNo;
                allSae[saeIdx].ReportingPeriod = allSbe[i].ReportingPeriod;
                allSae[saeIdx].PersonType = allSbe[i].PersonType;
                saeIdx++;
            }
        }

        Log($"รวม DS_SBE: {allSbe.Count} รายการ, DS_SAE: {allSae.Count} รายการ");

        // 10-11. Write outputs
        Directory.CreateDirectory(p.OutputDirectory);
        string sbePath = Path.Combine(p.OutputDirectory, $"DS_SBE_{ymCode}.xlsx");
        string saePath = Path.Combine(p.OutputDirectory, $"DS_SAE_{ymCode}.xlsx");

        Log($"กำลังเขียน {Path.GetFileName(sbePath)}...");
        SbeWriter.Write(allSbe, sbePath);

        Log($"กำลังเขียน {Path.GetFileName(saePath)}...");
        SaeWriter.Write(allSae, saePath);

        Log($"✅ เสร็จสิ้น! ไฟล์บันทึกที่: {p.OutputDirectory}");
        return (sbePath, saePath);
    }

    private static List<RspTransaction> LoadRsp(
        List<string> ibFiles, List<string> obFiles, Action<string> log)
    {
        var result = new List<RspTransaction>();
        foreach (var f in ibFiles)
        {
            var rows = RspReader.ReadFile(f, "IB");
            result.AddRange(rows);
            log($"  IB {Path.GetFileName(f)}: {rows.Count} รายการ");
        }
        foreach (var f in obFiles)
        {
            var rows = RspReader.ReadFile(f, "OB");
            result.AddRange(rows);
            log($"  OB {Path.GetFileName(f)}: {rows.Count} รายการ");
        }
        return result;
    }
}
