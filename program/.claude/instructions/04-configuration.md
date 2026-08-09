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

## Fixed, non-configurable paths

`MainForm` computes these relative to the repo root (5 directories above the
build output — `bin\Debug\net8.0-windows\` or `bin\Release\net8.0-windows\`
under `program\BotReport2026\`), **not** from `config.json`:

- `lookups/saction_list.xlsx` — sanction list
- `lookups/financial_crime_individuals_suspicious_counter_customer_behavior.xlsx` — crime/suspicious list
- `mapper/list_of_occupation_expected_monthly_income.xlsx` — occupation→income map
- `input-rsp/`, `input-transaction-report/` — input folders
- `report-results/` — output folder

If the folder structure changes, update `RepoRoot` in
[MainForm.cs](../BotReport2026/UI/MainForm.cs) — moving the executable's build
output depth (e.g. changing target framework or output path) will break this
relative path.
