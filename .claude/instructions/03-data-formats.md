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

- Date parsing tries `yyyy-MM-dd`, `dd-MM-yyyy`, `dd/MM/yyyy`, `M/d/yyyy`,
  `yyyy/MM/dd`, then a generic `DateTime.TryParse` fallback.
- Decimal parsing strips thousands-separator commas.

## Transaction Report — `TransactionReportReader`
[TransactionReportReader.cs](../BotReport2026/Readers/TransactionReportReader.cs)

- **Two** header rows (row 1 = "online" banner, row 2 = field names); data from
  row 3.
- Pre-filters at read time (nothing downstream needs to re-filter): keeps only
  rows where `cl24` (Error_Reason) contains "income"/"Income" **and** `cl4`
  (Status) is one of `Approved`, `Received`, `Delivered`.
- Reads `cl3` = MTCN, `cl9` = SenderName, `cl12` = SenderIdNumber, `cl17` =
  PrincipalAmount (null if ≤ 0), `cl24` = ErrorReason.
- Note: these column numbers (3/4/9/12/17/24) are specific to this reader and
  differ from the root [CLAUDE.md](../../../CLAUDE.md) shorthand (`cl4`=Status,
  `cl24`=Error_Reason) — the CLAUDE.md numbers match, but full column list isn't
  otherwise documented outside this reader.

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

## Outputs — `SbeWriter` / `SaeWriter`
[SbeWriter.cs](../BotReport2026/Writers/SbeWriter.cs),
[SaeWriter.cs](../BotReport2026/Writers/SaeWriter.cs)

- Plain `.xlsx`, one sheet each (`DS_SBE`, `DS_SAE`), bold white-on-blue header
  row, frozen header, auto-fit columns.
- `SbeRecord` → 17 columns in Thai (งวดข้อมูล, เลขที่อ้างอิง, ประเภทบุคคล, …,
  ยอดเงินรวม) — see [SbeRecord.cs](../BotReport2026/Models/SbeRecord.cs) for the
  underlying field list and defaults (`PersonType` defaults to "บุคคลธรรมดา",
  `FlagPersonType` to "1", `FlagSavingsAccount` to "1", `FlagEMoney` to "0").
- `SaeRecord` → 6 columns (เลขที่อ้างอิง, งวดข้อมูล, ประเภทบุคคล, วันที่ EDD
  เสร็จสิ้น, ผลการตรวจสอบ, รายละเอียดผู้ทำธุรกรรม).
- Written to `report-results/DS_SBE_{yyyyMM}.xlsx` and
  `report-results/DS_SAE_{yyyyMM}.xlsx`.
