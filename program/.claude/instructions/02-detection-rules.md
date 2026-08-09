# Detection Rules — Implementation Reference

All rules implement `IRuleEngine` ([IRuleEngine.cs](../BotReport2026/Rules/IRuleEngine.cs))
and receive a shared `RuleContext` with the reporting-month RSP, historical RSP,
transaction report rows, sanction data, crime/suspicious lists, occupation map,
`AppConfig`, reporting month, and a log callback. Each rule returns
`(List<SbeRecord>, List<SaeRecord>)`. Rules 201, 204, 205, 210 are out of scope and
have no implementation.

Person grouping in RSP-based rules always uses `PersonId` (IB: receiver ID / cl74,
OB: sender ID / cl39), case-insensitively trimmed.

**Inbound/outbound are evaluated independently in the five amount/count-aggregating
rules (202, 203-Retail, 206, 208, 209).** Each of these groups a person's
transactions by `PersonId` and then splits into `Direction == "IB"` /
`"OB"` sub-lists *before* computing totals, counts, or windows — an inbound
receipt and an outbound send under the same ID are never summed together.
The merge decision is centralized in
[DirectionalFlagResolver.cs](../BotReport2026/Rules/DirectionalFlagResolver.cs):
if only one direction breaches its threshold, the `SbeRecord` is built from
that direction's transactions alone and `RuleCode` gets a `-IB`/`-OB` suffix
(`BehaviorType` is unaffected); if **both** directions independently breach,
a single merged record is emitted covering both directions' transactions,
with the base `RuleCode` (no suffix) — same shape as the pre-split behavior.
Identity-matching rules (101, 212, 301) are unaffected — they match by
identity, not amount, so direction doesn't change their logic.

## Rule 101 — Sanction / CFR match
[Rule101SanctionMatch.cs](../BotReport2026/Rules/Rule101SanctionMatch.cs)

- Groups reporting-month RSP by `PersonId`.
- **TH/CFR list**: ID-only match (`ThSanctionIds`).
- **UN list**: ID **and** name match — checks `UnSanctionIds` for the ID, then
  requires the transaction's full name to substring-match one of the UN aliases
  for that ID (`UnSanctionNameToIds`, semicolon-separated aliases in the source
  sheet). ID-only UN matches are **not** flagged.
- All transactions for a hit person in the reporting month → one `SbeRecord`.

## Rule 202 — Abnormal monthly volume (rolling average)
[Rule202AbnormalVolume.cs](../BotReport2026/Rules/Rule202AbnormalVolume.cs)

- Builds `(personId, direction) → (year, month) → total principal` from
  `HistoricalRsp`, explicitly excluding the reporting month itself as a
  safety filter.
- Takes the `Config.Rule202PriorMonths` most recent months **that have activity**
  (months with zero transactions are simply absent from the map, so new customers
  with no history are skipped entirely) — computed separately per direction.
- Flags a direction if `reportingMonthTotal(direction) > average(priorMonths(direction)) × Config.Rule202Multiplier`.
- Defaults: 6 prior months, 3× multiplier.

## Rule 203 — Income/occupation mismatch
[Rule203IncomeMismatch.cs](../BotReport2026/Rules/Rule203IncomeMismatch.cs)

Two independent sub-checks, both tagged `BehaviorType = "203"` but different `RuleCode`:

- **203-Online**: every row already present in `ctx.TransactionReport` is flagged
  (the reader — see [03-data-formats.md](03-data-formats.md) — has already
  filtered to `Error_Reason` starting with "Income" and `Status` in
  Approved/Received/Delivered). Each row produces **both** an `SbeRecord`
  (`HasSae = true`) and a linked `SaeRecord` — this is the only rule that emits
  DS_SAE rows.
- **203-Retail**: combines reporting + historical RSP, excludes branches in
  `Config.Rule203ExcludedBranches` (default `ATH170025`, the online branch),
  restricts to a rolling window of `Config.Rule203RollingMonths` months back from
  the reporting month (default 3), groups by person, looks up occupation
  (falling back through `OccupationReader.FindExpectedIncome` — exact match →
  hardcoded fallback map → substring match; occupation is looked up once per
  person, not per direction), and flags a direction if that direction's window
  total exceeds `expectedIncome × Config.Rule203Multiplier` (default 3×). A
  direction only counts as breached if it also has reporting-month
  transactions to report. Only the **reporting-month** transactions for the
  breaching direction(s) are written to `SbeRecord` (the rolling window is
  only used to test the threshold).
- If no expected income can be resolved for an occupation, the person is skipped
  (logged, not flagged).

## Rule 206 — High-risk border branches
[Rule206HighRiskBranch.cs](../BotReport2026/Rules/Rule206HighRiskBranch.cs)

- No-op (logs and returns empty) if `Config.Rule206HighRiskBranches` is empty —
  currently true by default since there are no branches in the 3 southern border
  provinces.
- Otherwise filters reporting-month RSP to those branch codes, groups by person,
  splits into IB/OB, and flags a direction if `total principal ≥ Config.Rule206PrincipalThreshold`
  (default 700,000) **or** `transaction count ≥ Config.Rule206TxnCountThreshold`
  (default 20) — evaluated independently per direction.

## Rule 208 — Structuring
[Rule208Structuring.cs](../BotReport2026/Rules/Rule208Structuring.cs)

- Filters to sub-threshold transactions: `0 < Principal < Config.Rule208SubThreshold`
  (default 50,000, i.e. ≤ 49,999).
- Per person, splits into IB/OB, then independently checks each direction
  against three windows (any one triggers a flag, checked in this order —
  monthly, then daily, then weekly — first match wins):
  - Monthly: `count > Rule208MonthlyCount` (default 50) **and**
    `total ≥ Rule208MonthlyAmount` (default 150,000).
  - Daily (grouped by calendar date): `count > Rule208DailyCount` (default 10)
    **and** `total ≥ Rule208DailyAmount` (default 150,000).
  - Weekly (grouped by ISO week via `ISOWeek.GetWeekOfYear`):
    `count > Rule208WeeklyCount` (default 20) **and**
    `total ≥ Rule208WeeklyAmount` (default 150,000).
- All of the breaching direction's sub-threshold transactions in the
  reporting month are included in the flagged record (not just the
  triggering window).

## Rule 209 — 24/7 continuous transactions
[Rule209ContinuousTrading.cs](../BotReport2026/Rules/Rule209ContinuousTrading.cs)

- Requires both date and time to be parsed. Groups by person, splits into
  IB/OB, then within each direction groups by calendar date. For each
  direction/day with ≥2 transactions, computes the span between the earliest
  and latest transaction time for that direction only; if
  `span ≥ Config.Rule209ConsecutiveHours` (default 16), that direction/day's
  transactions are flagged (an IB receipt and an OB send on the same day no
  longer combine to form a span).
- A person can be flagged from multiple days within a direction; all flagged
  transactions for the breaching direction(s) across the month are combined
  into one `SbeRecord`.
- Note: despite the CLAUDE.md description of "16–24 consecutive hours", the
  implementation is a single configurable threshold (`≥ hours`), not a range.

## Rule 212 — Financial crime persons
[Rule212FinancialCrime.cs](../BotReport2026/Rules/Rule212FinancialCrime.cs)

- Matches RSP person name (first+last) against the `บุคคลอาชญากรรมทางการเงิน`
  sheet. Builds a full-name set plus a first-name-only fallback set (for entries
  in the source sheet that have no last name).
- No-op if the crime list is empty.

## Rule 301 — Suspicious storefront customers
[Rule301SuspiciousRetail.cs](../BotReport2026/Rules/Rule301SuspiciousRetail.cs)

- Matches the `พฤติกรรมลูกค้าหน้าร้านน่าสงสัย` sheet against reporting-month RSP
  using a **priority key per source entry**: MTCN first, else ID number, else
  full name (only one key type is built per entry, in that priority order).
- Matched transactions are deduped by `MTCN|Direction`, then grouped by
  `PersonId` (falling back to `MTCN:{mtcn}` as the group key if `PersonId` is
  blank).

## Output linkage

After all rules run, `ReportEngine` assigns `SBE{yyyyMM}{seq:D4}` reference
numbers in rule-execution order, then walks the SBE list again linking any
`HasSae == true` record (currently only 203-Online) to the next `SaeRecord` in
order, copying `ReferenceNo`, `ReportingPeriod`, and `PersonType` onto it.
