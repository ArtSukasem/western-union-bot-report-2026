"""Extract reference tables from the requirements v3 workbook into SAE-lookups/.

One-off/idempotent: re-run whenever requirements/version3/*.xlsx is updated.

Produces four lookup workbooks consumed by the DS_SBE / DS_SAE writers:
  address_branch.xlsx        <- sheet 'address branch'            (SAE field 15)
  edd_reasons.xlsx           <- sheet 'สาเหตุและความผิดปกติ'      (SAE field 7 dropdown)
  behavior_descriptions.xlsx <- sheet '(A) พฤติกรรมความเสี่ยง'    (SBE field 5)
  country_code.xlsx          <- curated here, not in the workbook (SBE field 7, SAE field 4)

The BoT classification enumerations (Identification Type Code, Occupation Code) are
fixed and live in code instead — see Readers/ClassificationTables.cs.

Usage:  python tools/extract_v3_lookups.py
"""

import glob
import os
import sys

import openpyxl

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT_DIR = os.path.join(REPO, "SAE-lookups")

# Country name -> ISO 3166-1 alpha-2. Seeded from the 247 distinct values found across
# RSP cl66/cl77 (inbound) and cl31/cl42 (outbound). Values that are already two-letter
# codes pass through in the reader and need no row here; the exceptions at the bottom
# are two-letter values that are NOT valid ISO2 and must be corrected.
COUNTRY_CODES = {
    "AFGHANISTAN": "AF", "ALAND ISLANDS": "AX", "ALBANIA": "AL", "ALGERIA": "DZ",
    "ANGOLA": "AO", "ARGENTINA": "AR", "ARMENIA": "AM", "AUSTRALIA": "AU",
    "AUSTRIA": "AT", "AZERBAIJAN": "AZ", "BAHRAIN": "BH", "BANGLADESH": "BD",
    "BELARUS": "BY", "BELGIUM": "BE", "BENIN": "BJ", "BHUTAN": "BT",
    "BOLIVIA": "BO", "BOSNIA AND HERZEGOVINA": "BA", "BRAZIL": "BR", "BULGARIA": "BG",
    "CAMBODIA": "KH", "CAMEROON": "CM", "CANADA": "CA", "CHILE": "CL",
    "CHINA": "CN", "COLOMBIA": "CO", "COMOROS": "KM",
    "CONGO DEMOCRATIC REPUBLIC OF": "CD", "CONGO-BRAZZAVILLE": "CG",
    "CROATIA": "HR", "CUBA": "CU", "CZECH REPUBLIC": "CZ", "DENMARK": "DK",
    "DOMINICAN REPUBLIC": "DO", "ECUADOR": "EC", "EGYPT": "EG", "ERITREA": "ER",
    "ESTONIA": "EE", "ETHIOPIA": "ET", "FINLAND": "FI", "FRANCE": "FR",
    "GABON": "GA", "GEORGIA": "GE", "GERMANY": "DE", "GHANA": "GH",
    "GREECE": "GR", "GUERNSEY": "GG", "GUINEA": "GN", "HAITI": "HT",
    "HONG KONG": "HK", "HUNGARY": "HU", "ICELAND": "IS", "INDIA": "IN",
    "INDONESIA": "ID", "IRAN": "IR", "IRAQ": "IQ", "IRELAND": "IE",
    "ISRAEL": "IL", "ITALY": "IT", "IVORY COAST": "CI", "JAPAN": "JP",
    "JORDAN": "JO", "KAZAKHSTAN": "KZ", "KENYA": "KE", "KOREA REP.": "KR",
    "KOSOVO": "XK", "KUWAIT": "KW", "KYRGHYZ REPUBLIC": "KG", "LAOS": "LA",
    "LATVIA": "LV", "LEBANON": "LB", "LIBERIA": "LR", "LIBYA": "LY",
    "LITHUANIA": "LT", "MACAU": "MO", "MADAGASCAR": "MG", "MALAYSIA": "MY",
    "MALDIVES": "MV", "MALI": "ML", "MAURITIUS": "MU", "MEXICO": "MX",
    "MOLDOVA": "MD", "MONACO": "MC", "MONGOLIA": "MN", "MONTENEGRO": "ME",
    "MOROCCO": "MA", "MYANMAR": "MM", "NEPAL": "NP", "NETHERLANDS": "NL",
    "NEW ZEALAND": "NZ", "NIGERIA": "NG", "NORTH IRELAND": "GB", "NORWAY": "NO",
    "OMAN": "OM", "PAKISTAN": "PK", "PALESTINIAN AUTHORITY": "PS", "PANAMA": "PA",
    "PAPUA NEW GUINEA": "PG", "PERU": "PE", "PHILIPPINES": "PH", "POLAND": "PL",
    "PORTUGAL": "PT", "QATAR": "QA", "ROMANIA": "RO", "RUSSIA": "RU",
    "SAMOA": "WS", "SAUDI ARABIA": "SA", "SENEGAL": "SN", "SERBIA": "RS",
    "SIERRA LEONE": "SL", "SINGAPORE": "SG", "SLOVAKIA": "SK", "SLOVENIA": "SI",
    "SOMALIA": "SO", "SOUTH AFRICA": "ZA", "SPAIN": "ES", "SRI LANKA": "LK",
    "SUDAN": "SD", "SWAZILAND": "SZ", "SWEDEN": "SE", "SWITZERLAND": "CH",
    "SYRIA": "SY", "TAIWAN": "TW", "TAJIKISTAN": "TJ", "TANZANIA": "TZ",
    "THAILAND": "TH", "TOGO": "TG", "TRINIDAD AND TOBAGO": "TT", "TUNISIA": "TN",
    "TURKEY": "TR", "TURKMENISTAN": "TM", "UGANDA": "UG", "UKRAINE": "UA",
    "UNITED ARAB EMIRATES": "AE", "UNITED KINGDOM": "GB", "UNITED STATES": "US",
    "UZBEKISTAN": "UZ", "VENEZUELA": "VE", "VIETNAM": "VN", "YEMEN": "YE",
    "ZAMBIA": "ZM", "ZIMBABWE": "ZW",
    # Three-letter form the spec calls out explicitly for the Thai check.
    "THA": "TH",
    # Non-ISO two-letter values present in the data; these override pass-through.
    "TP": "TL",  # former East Timor code
    "YU": "RS",  # former Yugoslavia
    "US/TX": "US",
}


def find_source():
    hits = glob.glob(os.path.join(REPO, "requirements", "version3", "*.xlsx"))
    if not hits:
        sys.exit("No .xlsx found in requirements/version3/")
    return hits[0]


def write_sheet(filename, sheet_name, headers, rows):
    wb = openpyxl.Workbook()
    ws = wb.active
    ws.title = sheet_name
    ws.append(headers)
    for cell in ws[1]:
        cell.font = openpyxl.styles.Font(bold=True)
    for row in rows:
        ws.append(row)
    ws.freeze_panes = "A2"
    path = os.path.join(OUT_DIR, filename)
    wb.save(path)
    print(f"  {filename}: {len(rows)} rows")
    return path


def extract_address_branch(src):
    """Sheet 'address branch': row 1 = headers, cl1 = account, cl2-cl8 = address parts."""
    ws = src["address branch"]
    rows = []
    for r in ws.iter_rows(min_row=2, max_col=8, values_only=True):
        account = (str(r[0]).strip() if r[0] else "")
        if not account:
            continue
        rows.append([account] + [str(c).strip() if c else "" for c in r[1:8]])
    return write_sheet(
        "address_branch.xlsx", "AddressBranch",
        ["account", "ชื่อสถานที่", "ที่ตั้งเคาน์เตอร์", "ที่อยู่ เลขที่",
         "แขวง / ตำบล", "เขต / อำเภอ", "จังหวัด", "รหัสไปรษณีย์"],
        rows)


def extract_edd_reasons(src):
    """Sheet 'สาเหตุและความผิดปกติ' column B: the EDD failure-reason dropdown options.

    Row 1 col B is the group heading, not an option. The sheet also carries a note
    about wanting a default reason later, so an IsDefault column is emitted now.
    """
    ws = src["สาเหตุและความผิดปกติ"]
    rows = []
    for i, r in enumerate(ws.iter_rows(min_row=2, max_col=2, values_only=True)):
        text = str(r[1]).strip() if len(r) > 1 and r[1] else ""
        if not text:
            continue
        # Stop before the trailing note row about configurable defaults.
        if text.startswith("และให้สามารถ"):
            break
        rows.append([text, ""])
    return write_sheet(
        "edd_reasons.xlsx", "EddReasons", ["เหตุผล", "IsDefault"], rows)


def extract_behavior_descriptions(src):
    """Sheet '(A) พฤติกรรมความเสี่ยง': col A = rule code, col B = description.

    Only numeric codes are kept — the sheet interleaves 'หมวด NN' category headings
    and continuation rows that have no code in column A.
    """
    ws = src["(A) พฤติกรรมความเสี่ยง"]
    rows = []
    for r in ws.iter_rows(min_row=2, max_col=2, values_only=True):
        code = str(r[0]).strip() if r[0] else ""
        desc = str(r[1]).strip().replace("\n", " ") if len(r) > 1 and r[1] else ""
        if not code.isdigit() or not desc:
            continue
        rows.append([code, desc])
    return write_sheet(
        "behavior_descriptions.xlsx", "BehaviorDescriptions",
        ["รหัสพฤติกรรม", "คำอธิบายพฤติกรรม"], rows)


def extract_country_codes():
    rows = sorted([name, iso] for name, iso in COUNTRY_CODES.items())
    return write_sheet(
        "country_code.xlsx", "CountryCode", ["CountryName", "ISO2"], rows)


def main():
    os.makedirs(OUT_DIR, exist_ok=True)
    path = find_source()
    print(f"Source: {os.path.basename(path)}")
    src = openpyxl.load_workbook(path, read_only=True, data_only=True)
    print(f"Writing to {OUT_DIR}")
    extract_address_branch(src)
    extract_edd_reasons(src)
    extract_behavior_descriptions(src)
    extract_country_codes()
    print("Done.")


if __name__ == "__main__":
    main()
