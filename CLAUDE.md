# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This is a **financial compliance automation** project for generating **Suspicious Behavior Reports (SBR)** submitted monthly to the Bank of Thailand (ธปท.) via the BOT Data Acquisition System. It processes money-transfer transaction data and flags suspicious behaviors per ธปท. SBR Dataset specification v2026.01.

There is **no existing code** — this project currently contains only input data, lookup tables, and requirements. Scripts must be built from scratch using Python.

## Output Reports

Two datasets are produced each month (due: Data Date + 15 days):

| Dataset | Code | Description |
|---|---|---|
| DS_SBE | 1.1 | ข้อมูลพฤติกรรมที่ผิดปกติรายลูกค้า (pre-EDD suspicious behavior) |
| DS_SAE | 1.2 | ผลการตรวจสอบ EDD (Enhanced Customer Due Diligence results) |

## Input Files

| Directory | File Pattern | Description |
|---|---|---|
| `input-rsp/` | `RSP_Inbound_Mmmyy.xlsx`, `RSP_Outbound_Mmmyy.xlsx` | Money-transfer transactions (121 columns). Covers both retail and online channels. |
| `input-transaction-report/` | `Transaction-Report Mmmyy.xls.xlsx` | Online channel transactions (30 columns). Used only for rule 203-online. |
| `lookups/saction-list.xlsx` | — | Sanction list with two sheets: **UNSanctionList** (cl5=FIRST_NAME, cl10=NUMBER) and **THSanctionList** / CFR (cl10=IDENTITY_ID). |
| `lookups/ข้อมูลบุคคลอาชญากรรมทางการเงินและพฤติกรรมลูกค้าหน้าร้านน่าสงสัย.xlsx` | — | Sheet **พฤติกรรมลูกค้าหน้าร้านน่าสงสัย** (MTCN, Firstname, Lastname, IDnumber); sheet **บุคคลอาชญากรรมทางการเงิน** (Firstname, Lastname). |
| `mapper/List of occupation & expect monthly income.xlsx` | — | Maps occupation name (cl1) to expected monthly income in THB (cl6). Used for rule 203. |

## RSP Column Reference

RSP files have 121 columns. Key columns used across rules:

**Inbound (Receive money — IB):**
- cl1 = MTCN, cl5 = Pay_Date, cl6 = Pay_Time, cl12 = Pay_Principal
- cl22 = Pay_Agent_Account_ID (branch code; use to exclude online branch `ATH170025`)
- cl60 = Receiver_First_Name, cl61 = Receiver_Last_Name
- cl74 = Receiver_Id1_Number, cl89 = Receiver_Occupation (use cl114 if value is "Other/OTHERS")

**Outbound (Send money — OB):**
- cl1 = MTCN, cl3 = Send_Date, cl4 = Send_Time, cl8 = Send_Principal
- cl16 = Send_Agent_Account_ID (branch code; use to exclude `ATH170025`)
- cl25 = Sender_First_Name, cl26 = Sender_Last_Name
- cl39 = Sender_Id1_Number, cl54 = Sender_Occupation (use cl104 if value is "Other/OTHERS")

**Transaction Report columns:**
- cl4 = Status, cl24 = Error_Reason

## Detection Rules

> Rules 201, 204, 205, 210 are explicitly **out of scope** ("ไม่เข้าเกณฑ์").

### Rule 101 — Sanction / CFR List Match
- Match RSP sender/receiver name and ID against both UN and TH sanction lists for the reporting month.
- IB: match Receiver Firstname (cl60), Lastname (cl61), ID (cl74) against lists.
- OB: match Sender Firstname (cl25), Lastname (cl26), ID (cl39) against lists.
- Matching logic: name **and** ID match → flag all transactions for that person → DS_SBE.

### Rule 202 — Abnormal Monthly Volume (Rolling Average)
- Load all RSP files for N months prior + reporting month (default: 6 prior months; **user-configurable**).
- Group by person ID (IB: cl74; OB: cl39). Compute average monthly principal for the prior N months (skip months with zero activity — new customers excluded).
- If reporting-month total principal > **3× average** (multiplier **user-configurable**) → flag all that person's transactions for the reporting month → DS_SBE.

### Rule 203 — Transactions Inconsistent with Income / Occupation Profile
**Online channel (Transaction Report):**
- Filter rows where cl24 (Error Reason) contains text starting with `Income` (e.g., `r2110 proof of income required`).
- If cl4 (Status) is `Approved`, `Received`, or `Delivered` → EDD already passed → report both DS_SBE **and** DS_SAE.

**Retail channel (RSP only — exclude branch `ATH170025`):**
- Rolling 3 months (prior months; **user-configurable**).
- Look up occupation from cl54/cl89 (use cl104/cl114 if "Other") → find expected monthly income from mapper file (cl6).
- If total principal in rolling 3 months > **3× expected monthly income** (multiplier **user-configurable**) → DS_SBE.
- Branch account exclusion list is **user-configurable**.

### Rule 206 — Transactions in High-Risk Border Branches
- Currently no qualifying branches (no branches in 3 southern border provinces). Branch list is **user-configurable** for future use.
- Threshold: monthly principal ≥ **700,000 THB** or transaction count ≥ **20 times/month** (both **user-configurable**) → DS_SBE.

### Rule 208 — Structuring (High frequency, sub-threshold amounts)
- Filter transactions with principal < 50,000 THB (i.e., ≤ 49,999 THB).
- Flag if any of the following in the reporting month (**all thresholds user-configurable**):
  - > 8–10 times/day **and** daily total ≥ 150,000 THB
  - > 15–20 times/week **and** weekly total ≥ 150,000 THB
  - > 30–50 times/month **and** monthly total ≥ 150,000 THB
- → DS_SBE.

### Rule 209 — 24/7 Continuous Transactions
- Detect persons making transactions across ≥ **16–24 consecutive hours** within a single day (**threshold user-configurable**).
- → DS_SBE.

## Python Environment

```bash
pip install openpyxl pandas pypdf pdfplumber
```

All scripts should use `encoding='utf-8'` when writing files. Column headers in RSP/Transaction Report files have leading spaces — strip them when reading. RSP files have a **single header row** (row 1 = field names); data starts at row 2. Transaction Report files have **two header rows** (row 1 = "online", row 2 = field names); data starts at row 3. Header text is ignored — columns are indexed by position (cl1…cl121) as documented above.
