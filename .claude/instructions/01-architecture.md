# BotReport2026 — Architecture

C# WinForms desktop app (.NET 8, `net8.0-windows`) that generates the two monthly
SBR datasets (DS_SBE, DS_SAE) from RSP / Transaction Report / lookup Excel files.
Project file: [BotReport2026.csproj](../BotReport2026/BotReport2026.csproj).

This implements the same business rules documented in the repo-root
[CLAUDE.md](../../../CLAUDE.md), but as a compiled Windows app instead of Python
scripts — that root doc's "no existing code / Python" framing predates this project.

## Dependencies

- **EPPlus** (6.x) — reads/writes `.xlsx` files. License set to `NonCommercial` in
  [Program.cs](../BotReport2026/Program.cs).
- **Newtonsoft.Json** (13.x) — serializes `config.json` for rule thresholds.

## Project layout

| Folder | Responsibility |
|---|---|
| `Models/` | Plain data classes: `RspTransaction`, `TransactionReportRow`, `SbeRecord`, `SaeRecord`, `AppConfig`. |
| `Readers/` | Excel parsing: `RspReader`, `TransactionReportReader`, `SanctionListReader`, `CrimeSuspiciousReader`, `OccupationReader`, `InputFolderScanner`. |
| `Rules/` | One class per detection rule, all implementing `IRuleEngine`. |
| `Engine/` | `ReportEngine` (orchestrator) and `RunParameters` (input bundle for a run). |
| `Writers/` | `SbeWriter`, `SaeWriter` — write the two output `.xlsx` datasets. |
| `Config/` | `ConfigManager` — load/save `config.json` next to the executable. |
| `UI/` | WinForms UI: `MainForm` + three tabs (`TabFileSelection`, `TabSettings`, `TabRunOutput`). |

## Control flow

1. `Program.Main` → `Application.Run(new UI.MainForm())`.
2. `MainForm` constructor resolves repo-root-relative paths (5 levels up from the
   build output dir — see `RepoRoot` in [MainForm.cs](../BotReport2026/UI/MainForm.cs)),
   ensures the standard folders exist, and auto-scans `input-rsp-inbound/`,
   `input-rsp-outbound/` and `input-transaction-report/` via `InputFolderScanner`
   to pick the latest reporting month.
3. User reviews/adjusts file selection (`TabFileSelection`) and rule settings
   (`TabSettings`), then clicks **รันรายงาน** (Run) in `TabRunOutput`.
4. `MainForm.StartRun` builds a `RunParameters` and calls `ReportEngine.RunAsync`
   on a background thread, streaming log lines back to the UI via a callback.
5. `ReportEngine.Run` (see [ReportEngine.cs](../BotReport2026/Engine/ReportEngine.cs)):
   - Loads the occupation map, sanction lists, crime/suspicious-retail lists.
   - Loads reporting-month RSP and historical RSP (both IB and OB).
   - Loads pre-filtered Transaction Report rows.
   - Builds a shared `RuleContext` ([IRuleEngine.cs](../BotReport2026/Rules/IRuleEngine.cs)).
   - Runs all 8 rule engines in a fixed sequence, collecting `SbeRecord`/`SaeRecord`.
   - Assigns sequential `SBE{yyyyMM}{seq:D4}` reference numbers, then links SAE rows
     back to their SBE reference (only for rows where `SbeRecord.HasSae == true`,
     i.e. Rule 203-Online).
   - Writes `DS_SBE_{yyyyMM}.xlsx` and `DS_SAE_{yyyyMM}.xlsx` to `report-results/`.

## Adding a new rule

1. Add a class in `Rules/` implementing `IRuleEngine` (`RuleCode` + `Execute`).
2. Register it in the `_rules` list in
   [ReportEngine.cs](../BotReport2026/Engine/ReportEngine.cs).
3. If it needs new tunable thresholds, add properties to
   [AppConfig.cs](../BotReport2026/Models/AppConfig.cs) and a matching settings
   group in [TabSettings.cs](../BotReport2026/UI/TabSettings.cs) (`GetConfig`/`SetConfig`
   must both be updated).
4. See [02-detection-rules.md](02-detection-rules.md) for the pattern each rule follows.
