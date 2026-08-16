# UI Structure & User Workflow

`MainForm` ([MainForm.cs](../BotReport2026/UI/MainForm.cs) +
[MainForm.Designer.cs](../BotReport2026/UI/MainForm.Designer.cs)) hosts three
tabs as `UserControl`s: `_tabFileSelection`, `_tabSettings`, `_tabRunOutput`.

## Tab 1 — File Selection
[TabFileSelection.cs](../BotReport2026/UI/TabFileSelection.cs)

- On load and on "🔄 โหลดใหม่" (reload), scans `input-rsp-inbound/`,
  `input-rsp-outbound/` and `input-transaction-report/` via `InputFolderScanner`,
  storing the full unfiltered list of IB/OB/Transaction Report files. IB vs OB
  comes from the folder, not the file name.
- `ApplyReportingMonth(month)` buckets those files into **reporting** (exact
  year+month match) vs **historical** (earlier month, or month couldn't be
  parsed from the filename — treated conservatively as historical) and refreshes
  five list boxes: Reporting IB/OB, Transaction Report, Historical IB/OB.
- Shows ✅/❌ status for the three reference files (occupation mapper, sanction
  list, crime/suspicious list) via `RefreshReferenceStatus()` — a red ❌ means
  that rule's dependent checks will be skipped or fail, not a hard error.
- "📂 เปิดโฟลเดอร์ ..." buttons just open Explorer at the relevant folder;
  there's no in-app file picker — files must be placed in the folders directly.

## Tab 2 — Settings
[TabSettings.cs](../BotReport2026/UI/TabSettings.cs)

- One `GroupBox` per rule (see [04-configuration.md](04-configuration.md) for the
  full field list and defaults). Rules 101/212/301 show as info-only groups
  (no editable fields) since they have no thresholds.
- "💾 บันทึกค่าตั้ง" saves immediately to `config.json`; "📂 โหลดค่าตั้ง" reloads
  from disk, discarding unsaved edits in the form.
- Settings are re-read (`GetConfig()`) and saved again automatically at the
  start of every run, regardless of whether the user clicked save.

## Tab 3 — Run & Output
[TabRunOutput.cs](../BotReport2026/UI/TabRunOutput.cs)

- Month picker (`MM/yyyy`) — changing it calls
  `MainForm.OnReportingMonthChanged`, which re-buckets File Selection's lists
  for the new month (this is the single source of truth for "reporting month";
  Tab 1's files auto-adjust to match it).
- "▶ รันรายงาน" (Run) is disabled while a run is in progress; "ยกเลิก" (Cancel)
  triggers a `CancellationTokenSource` that `ReportEngine` checks between major
  steps and before each rule (not mid-rule).
- Log output streams into a black/green `RichTextBox` (console-style) via
  `AppendLog`, marshaled onto the UI thread with `Invoke` when called from the
  background task.
- On success, shows both output file paths and enables "📂 เปิดโฟลเดอร์ผลลัพธ์"
  to open `report-results/` in Explorer.
- Validation: Run refuses to start if all three of Reporting IB, Reporting OB,
  and Transaction Report file lists are empty (see `MainForm.StartRun`) — it
  does **not** check that the reference files (sanction list, mapper, crime
  list) exist before running; missing reference files instead cause individual
  rules to skip/log rather than blocking the whole run (except sanction list
  and occupation map, which are read unconditionally in
  `ReportEngine.Run` and will throw if the file itself is missing).

## End-to-end user flow

1. Drop this month's RSP inbound file into `input-rsp-inbound/`, the outbound
   file into `input-rsp-outbound/`, and `Transaction-Report Mmmyy.xls` into
   `input-transaction-report/`, plus prior months' RSP files needed for Rules
   202/203's rolling windows. File names must still contain the `Mmmyy` month
   token.
2. Launch the app — it auto-detects the latest month across all scanned files
   and sets that as the reporting month.
3. Confirm the reporting month in Tab 3 (or change it, which re-buckets Tab 1).
4. Check Tab 1's ✅/❌ reference-file status and file listings.
5. Optionally adjust thresholds in Tab 2.
6. Run in Tab 3; watch the log; open the output folder when done.
