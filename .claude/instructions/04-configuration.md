# Configuration — `AppConfig` / `config.json`

Defined in [AppConfig.cs](../BotReport2026/Models/AppConfig.cs), persisted as
`config.json` next to the executable via
[ConfigManager.cs](../BotReport2026/Config/ConfigManager.cs)
(`AppDomain.CurrentDomain.BaseDirectory/config.json`). If the file is missing or
fails to parse, `ConfigManager.Load()` silently falls back to `new AppConfig()`
defaults — there is no error surfaced to the user in that case.

The Settings tab ([TabSettings.cs](../BotReport2026/UI/TabSettings.cs)) is the UI
for editing these values; `GetConfig()`/`SetConfig()` there must stay in sync with
every property added to `AppConfig`. Settings are also saved automatically at the
start of every run (`MainForm.StartRun`), not only when the user clicks "บันทึกค่าตั้ง".

## Fields and defaults

| Property | Default | Used by |
|---|---|---|
| `RspIncludedStatuses` | `["PAID", "UNPAID"]` | RSP read (all rules) — cl2 filter; empty list = no filtering. Sheet (A) requires both values |
| `SaeOnlineChannelBranches` | `["ATH170025", "ATH170014"]` | DS_SAE fields 11 & 15 — treated as the online channel (`330004`) with no physical location. Note this is a **different list** from `Rule203ExcludedBranches`, which has only `ATH170025` |
| `Rule202PriorMonths` | 6 | Rule 202 |
| `Rule202Multiplier` | 3 | Rule 202 |
| `Rule203RollingMonths` | 3 | Rule 203 (retail) |
| `Rule203Multiplier` | 3 | Rule 203 (retail) |
| `Rule203ExcludedBranches` | `["ATH170025"]` | Rule 203 (retail) — branches treated as online, excluded from retail check |
| `Rule206HighRiskBranches` | `[]` (empty) | Rule 206 — empty means the rule is a no-op |
| `Rule206PrincipalThreshold` | 700000 | Rule 206 |
| `Rule206TxnCountThreshold` | 20 | Rule 206 |
| `Rule208SubThreshold` | 50000 | Rule 208 — transactions strictly below this are "sub-threshold" |
| `Rule208DailyCount` / `Rule208DailyAmount` | 10 / 150000 | Rule 208 daily check |
| `Rule208WeeklyCount` / `Rule208WeeklyAmount` | 20 / 150000 | Rule 208 weekly check |
| `Rule208MonthlyCount` / `Rule208MonthlyAmount` | 50 / 150000 | Rule 208 monthly check |
| `Rule209ConsecutiveHours` | 16 | Rule 209 |

Rules 101, 212, and 301 have no tunable thresholds — they only depend on the
sanction list, financial-crime list, and suspicious-retail list files being
present (`TabSettings` shows them as informational groups only).

## Paths — the working folder

Every input, lookup and output path is resolved under a single **working folder**
the user picks from the bar at the top of the main window
([WorkspaceBar.cs](../BotReport2026/UI/WorkspaceBar.cs)). `Workspace`
([Workspace.cs](../BotReport2026/Config/Workspace.cs)) owns that root and the fixed
sub-folder ("job") name each part of the run uses inside it:

| Sub-folder | Holds |
|---|---|
| `input-rsp-inbound/`, `input-rsp-outbound/` | RSP input (direction comes from the folder) |
| `input-transaction-report/` | Transaction Report input |
| `lookups/` | `saction_list.xlsx`, `financial_crime_individuals_suspicious_counter_customer_behavior.xlsx` |
| `mapper/` | `list_of_occupation_expected_monthly_income.xlsx` |
| `SAE-lookups/` | DS_SBE/DS_SAE reference tables; regenerate with `python tools/extract_v3_lookups.py` |
| `report-results/` | `DS_SBE_*.csv`, `DS_SAE_*.xlsm`, `error.xlsx` |

`MainForm`'s static path properties (`InputRspInboundDir`, `SanctionListPath`,
`OutputDirectory`, …) are now thin forwarders to `Workspace`, so call sites did not
change. `Workspace.EnsureFolders()` creates the whole set whenever the root is set.

### Where the root comes from

`Workspace` resolves it once at startup, first match wins:

1. `workspace.json` beside the executable (written by `Workspace.SetRoot`), if that
   folder still exists — a deleted folder or disconnected drive falls through
   rather than leaving every path dangling.
2. The repo checkout: walks up to 6 levels from the build output looking for a
   folder that already contains `input-rsp-inbound/` or `lookups/`. This keeps a
   dev build (`bin/Debug/net8.0-windows/`) using the repo's folders as before.
3. `bot-report-files/` beside the executable.

`workspace.json` is deliberately **separate** from `config.json`: `TabSettings.GetConfig()`
rebuilds `AppConfig` from its controls on every save, so a path property added to
`AppConfig` would be wiped the first time the user saved settings. `config.json`
itself stays beside the executable — it is per-install, not per-working-folder.

### Switching folders

`MainForm.ApplyWorkspaceChange(previousRoot)` runs on every change: it drops the
engine's cached `RuleContext` (it belongs to the old folder), rescans the new
input folders, re-points the output label, and — when the new folder has none of
the reference files — offers to copy `lookups/`, `mapper/` and `SAE-lookups/`
across from the previous folder (`Workspace.CopyReferenceFiles`, which never
overwrites an existing file). Those tables are per-install data, not per-month,
so a freshly created folder would otherwise start empty and fail mid-run.
