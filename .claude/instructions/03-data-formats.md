# Input/Output File Formats

## RSP files — `RspReader`
[RspReader.cs](../BotReport2026/Readers/RspReader.cs)

- Single header row (row 1); data starts at row 2. First worksheet only.
- Columns are read by **position**, not header text. A row is skipped if `cl1`
  (MTCN) is blank, or if parsed `Principal <= 0`.
- Column mapping is direction-dependent (`"IB"` vs `"OB"` passed in by the caller):

  | Field | IB (Inbound / receive) | OB (Outbound / send) |
  |---|---|---|
  | Date | cl5 | cl3 |
  | Time | cl6 | cl4 |
  | Principal | cl12 | cl8 |
  | Branch/account | cl22 | cl16 |
  | First name | cl60 | cl25 |
  | Last name | cl61 | cl26 |
  | Person ID | cl74 | cl39 |
  | Occupation | cl89 (fallback cl114 if "Other/OTHERS") | cl54 (fallback cl104) |

- Date parsing tries `MM/dd/yyyy`, `M/d/yyyy`, `yyyy-MM-dd`, `yyyy/MM/dd`,
  `dd-MM-yyyy`, then a generic `DateTime.TryParse` fallback. `dd/MM/yyyy` is
  deliberately **not** in the list — RSP dates are US-order, and accepting both
  would silently swap day and month on the days where either parse succeeds.
- Decimal parsing strips thousands-separator commas.

## RSP CSV parsing — `CsvUtil`
[CsvUtil.cs](../BotReport2026/Readers/CsvUtil.cs)

A `"` opens a quoted field **only at the start of a field**; anywhere else it is an
ordinary character. This matches Excel and RFC 4180, and it is not optional — RSP
exports put bare quotes in the middle of unquoted fields, in two forms:

- free text, e.g. a reason field reading `REFUND THE DEPOSIT" "RETURN THE DEPOSIT"`
- the Excel-escape form `="0956506318"` used on phone/ID columns so Excel keeps the
  leading zero

Treating those as real quote marks makes the parser swallow the commas that follow —
and then the line break too — so rows merge and every column after the stray quote
shifts. Because the `="..."` fields keep re-opening the quote on each following line,
one odd quote does not resolve itself: it eats the **entire rest of the file**. That
is exactly what happened to `..._Inbound_Mar26.csv`, where row 5276 carried the
`REFUND THE DEPOSIT"` text and the reader produced 5,276 rows instead of 9,759 —
silently, since a short file looks like a small month.

Unwrapping `="..."` is `Clean`'s job, not the parser's: `Clean` trims surrounding
whitespace (dates arrive as `" 03/23/2026"`) and strips the `="` / `"` wrapper.

## Transaction Report — `TransactionReportReader`
[TransactionReportReader.cs](../BotReport2026/Readers/TransactionReportReader.cs)

- Header row count **varies between exports**, so `FindFirstDataRow` locates the
  row whose `cl3` reads "MTCN" and starts after it (falling back to row 2). The
  production Jan26 export has a single header row; earlier sample files had two
  (row 1 = "online" banner). Assuming two unconditionally silently dropped the
  first data row.
- Pre-filters at read time (nothing downstream needs to re-filter): keeps only
  rows where `cl24` (Error_Reason) contains "income"/"Income" **and** `cl4`
  (Status) is one of `Approved`, `Received`, `Delivered`.
- Reads `cl3` = MTCN, `cl9` = SenderName, `cl12` = SenderIdNumber, `cl17` =
  PrincipalAmount (null if ≤ 0), `cl24` = ErrorReason. Verified against the real
  Jan26 header row.
- A month with no income-flagged rows is normal, not a failure: Jan26 has 30,927
  data rows and **zero** error reasons mentioning income, so Rule 203-Online
  legitimately contributes nothing.

## Sanction list — `SanctionListReader`
[SanctionListReader.cs](../BotReport2026/Readers/SanctionListReader.cs)

- Two sheets, matched by worksheet name containing `"UN"` / `"TH"`
  (case-insensitive). Both: row 1 = title, row 2 = headers, data from row 3.
- **UN sheet**: `cl5` = name (semicolon-separated aliases), `cl10` = ID. Produces
  `UnSanctionIds` (set of IDs) and `UnSanctionNameToIds` (ID → alias set).
- **TH/CFR sheet**: `cl10` = ID only → `ThSanctionIds`.
- Logs progress every 100,000 rows since this list can be large.

## Crime / suspicious-retail list — `CrimeSuspiciousReader`
[CrimeSuspiciousReader.cs](../BotReport2026/Readers/CrimeSuspiciousReader.cs)

- Single header row; data from row 2. Missing file → empty result (logged, not
  an error).
- Sheet matched by name containing `"อาชญากรรม"` → `CrimePerson(FirstName,
  LastName)` list (cl1, cl2) for Rule 212.
- Sheet matched by name containing `"พฤติกรรม"` or `"หน้าร้าน"` →
  `SuspiciousRetail(MTCN, FirstName, LastName, IdNumber)` list (cl1–cl4) for
  Rule 301.

## Occupation map — `OccupationReader`
[OccupationReader.cs](../BotReport2026/Readers/OccupationReader.cs)

- Sheet matched by name containing `"Occupation"`. Single header row; data from
  row 2. `cl1` = occupation name, `cl6` = expected monthly income.
- `FindExpectedIncome` resolves an RSP occupation string in three steps: exact
  dictionary match → a hardcoded `FallbackMap` (handles known RSP↔mapper naming
  mismatches like `HOUSEWIFE` → `Housewife/Child Care`) → case-insensitive
  substring match in either direction.

## Input folder scanning — `InputFolderScanner`
[InputFolderScanner.cs](../BotReport2026/Readers/InputFolderScanner.cs)

- Parses the month from filenames via regex `([A-Za-z]{3})[ _]?(\d{2})` (e.g.
  `Apr26` → April 2026). Files whose month can't be parsed are treated as
  historical (conservative) by `TabFileSelection.ApplyReportingMonth`.
- `ScanRsp` takes the two RSP folders and returns `(inbound, outbound)`; **the
  folder decides the direction** — every file in `input-rsp-inbound/` is inbound
  and every file in `input-rsp-outbound/` is outbound, with no name matching at
  all. `ScanTxnReport` still matches names starting with `Transaction-Report` in
  `input-transaction-report/`. Extension doesn't matter anywhere — EPPlus reads by
  content, not by file extension. Excel lock files (`~$...`) are skipped.
- Row counts for display use `ws.Dimension.Rows - headerRows` (2 for RSP, 3 for
  Transaction Report) without reading cell data.

## Reference tables — `ReferenceDataReader`
[ReferenceDataReader.cs](../BotReport2026/Readers/ReferenceDataReader.cs)

Four lookup workbooks in `SAE-lookups/`, generated from the requirements v3
workbook by [tools/extract_v3_lookups.py](../../tools/extract_v3_lookups.py).
Re-run that script whenever `requirements/version3/` changes. Each is
single-header-row; a missing file is logged and leaves its table empty.

| File | Contents | Used by |
|---|---|---|
| `address_branch.xlsx` | account → 7 address parts (80 branches) | DS_SAE field 15 |
| `country_code.xlsx` | country name → ISO2 (146 entries) | DS_SBE 7, DS_SAE 4 |
| `behavior_descriptions.xlsx` | rule code → คำอธิบายพฤติกรรม | DS_SBE field 5 |
| `edd_reasons.xlsx` | 8 dropdown options | DS_SAE field 7 |

`ReferenceData.ToIso2` exists because RSP mixes formats in the same column —
`cl77` holds both `"THAILAND"` and `"TH"`. The lookup file wins first (it also
corrects non-ISO values like `TP`→`TL`), then any remaining two-letter value
passes through, then `""`. Applying the spec's literal `== "TH"` test instead
would misclassify thousands of Thai customers as foreign.

Fixed BoT enumerations (Identification Type Code, Occupation Code, counterparty /
channel / transaction-type codes) are in
[ClassificationTables.cs](../BotReport2026/Readers/ClassificationTables.cs)
instead — they are stable, unlike branches and countries.

## Outputs — `SbeWriter` / `SaeWriter` / `ErrorWriter`
[SbeWriter.cs](../BotReport2026/Writers/SbeWriter.cs),
[SaeWriter.cs](../BotReport2026/Writers/SaeWriter.cs)

Layouts follow requirements v3 sheets `(DS_SBE)` and `(DS_SAE)`.

- **`DS_SBE_{yyyyMM}.csv`** — 23 columns, **one row per MTCN**, no header row,
  written straight as the submission CSV. There is no .xlsx step because every field is derived —
  nothing here is for staff to fill in. Field 3 (เลขที่อ้างอิง) *is* the MTCN, and
  the BoT primary key is รหัสสถาบัน + งวดข้อมูล + เลขที่อ้างอิง, so
  `ReportEngine.Deduplicate` keeps one row per MTCN, first rule in `_rules` order
  winning, logging each collision.
- **`DS_SAE_{yyyyMM}.xlsm`** — macro-enabled, because staff complete the EDD
  columns before it can be submitted. 17 columns: fields 1–16 plus a **7.1
  free-text helper** at column 8, immediately after field 7.
  - **Row 1 is a toolbar row** holding the export button, so headers are on
    **row 2** and data starts on **row 3**; the frozen pane keeps the button
    visible. `SaeWriter.HeaderRow` is the single source of truth for this and is
    passed into the VBA generator, so the two cannot drift apart.
  - The header row carries an **autofilter** over `A{HeaderRow}:Q{lastRow}`.
    It is applied **before** `ApplyProtection` on purpose: a protected sheet lets
    staff *use* an autofilter (`AllowAutoFilter`) but never *create* one, so a
    filter added afterwards would be dead. Filtering only hides rows in the view —
    the export macro walks every row from `HEADER_ROW` to the last used row, so a
    filtered view never silently shortens the CSV.
  - Columns F and G carry Excel list validations sourced from a hidden
    `_options` sheet — not inline lists, because the EDD reasons exceed Excel's
    255-character inline limit. EPPlus emits these as `x14` extension validations
    since they reference another sheet; Excel honours them.
  - Rule 101 rows are pre-answered (result `"1"`, reason "รายชื่ออยู่ใน CFR") and
    their result cell is **locked**; every other cell is explicitly unlocked
    before the sheet is protected, since Excel locks all cells by default.
    `AllowEditObject` is on so the button stays clickable under protection.
  - Rule 203-Online rows are pre-filled with result `"0"` but left editable —
    sheet (A) says those already cleared EDD, while the DS_SAE sheet only
    mandates locking for 101.
- **`error.xlsx`** — MTCN / Citizen ID / Error Message / **Source File** / **Row**,
  written only when a `2002700001` (Personal Id) row's ID is not exactly 13 digits.
  A **warning**: the row is still reported in DS_SBE. A stale file from a previous
  run is deleted.
  - Source File and Row come from `RspTransaction.SourceFile` / `.SourceRow`,
    captured by `RspReader` while streaming. **Row is 1-based including the header**,
    so it matches what Excel shows when the CSV is opened — jump to that row and the
    offending value is right there. Both are blank when the record has no RSP row
    behind it (Rule 203-Online), though those never reach this check since their
    ID type is not `2002700001`.

## DS_SAE export button — `SaeVbaMacro`
[SaeVbaMacro.cs](../BotReport2026/Writers/SaeVbaMacro.cs)

The DS_SAE CSV is produced by VBA **inside the workbook**, not by the generator,
so staff never have to return to the app after filling in the EDD columns. The
generator embeds a `BotReportExport` module plus a form-control button wired to
it via `ExcelControl.Macro` (which EPPlus writes as `<x:FmlaMacro>` in the
control's VML — that is where Excel reads form-control macros from; the empty
`macro=""` on the DrawingML `<xdr:sp>` is expected and unused).

On click it writes `DS_SAE_{yyyyMM}.csv` next to the workbook, applying the same
rules the spec asks for: fold 7.1 into field 7 and drop the helper column
(16 columns out), skip the header row, collapse the result dropdown label to its
leading digit
(`Char(1)`), emit raw values rather than displayed text, and normalise dates.

Three things in that macro are deliberate and easy to break:

- **It is ASCII-only.** VBA module source is stored in the project's code page, so
  Thai literals would not survive. For the same reason the "other reason" case is
  detected by the 7.1 helper cell being non-empty rather than by matching the Thai
  dropdown text. Thai belongs on the button caption and in the worksheet, which
  are XML and safe.
- **Dates are built from `Year()`/`Month()`/`Day()`**, never `Format(d, "yyyy-mm-dd")`
  — a Buddhist-era regional setting would otherwise emit 2569 for 2026.
- **It writes UTF-8 with a BOM** via `ADODB.Stream`. See the encoding note below.

Excel will block the macro until the user enables content or the output folder is
a Trusted Location — the workbook is unsigned.

## CSV delimiter, quoting and header row

Both CSVs are **data rows only — no header row**. DS_SBE never writes
`SbeWriter.Headers` (kept purely as a field-name reference), and the DS_SAE macro
starts its loop at `HEADER_ROW + 1`; the *workbook* still shows its header row on
row 2, since staff need it while filling in the EDD columns.

Both use a **pipe (`|`) field separator**, and **every field is wrapped in
double quotes unconditionally** — not quote-only-when-needed. A field containing
a pipe, a line break, or nothing at all therefore looks the same to the reader.
Embedded quotes are doubled, as in RFC 4180.

Implemented in `CsvFile.Delimiter` / `CsvFile.Escape`
([CsvFile.cs](../BotReport2026/Writers/CsvFile.cs)) for DS_SBE, and mirrored by
the `Escape` + `Join(parts, "|")` pair in the DS_SAE macro — change one and the
other has to follow.

## CSV encoding

Both CSVs are **UTF-8 with BOM** ([CsvFile.cs](../BotReport2026/Writers/CsvFile.cs)
for DS_SBE, `ADODB.Stream` with `Charset = "utf-8"` for DS_SAE). Without the BOM,
Excel on a Thai Windows opens the file as CP874 and every Thai character is
mangled; UTF-8 consumers ignore the BOM. Do not "fix" this back to BOM-less.
