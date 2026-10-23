namespace BotReport2026.Models;

public class RspTransaction
{
    public string MTCN { get; set; } = "";
    public string Direction { get; set; } = "";       // "IB" or "OB"
    public DateTime? TransactionDate { get; set; }
    public TimeSpan? TransactionTime { get; set; }
    public decimal Principal { get; set; }
    public string BranchAccountId { get; set; } = "";
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string PersonId { get; set; } = "";
    public string Occupation { get; set; } = "";
    public string SourceFile { get; set; } = "";
    /// <summary>1-based line number in <see cref="SourceFile"/>, as Excel shows it
    /// (row 1 = header). Reported in error.xlsx so a bad value can be found.</summary>
    public int SourceRow { get; set; }

    // --- DS_SBE / DS_SAE v3 fields ---
    /// <summary>cl78 / cl43 — WU code: A = passport, B = personal ID.</summary>
    public string IdType { get; set; } = "";
    /// <summary>cl77 / cl42 — country that issued the ID; raw value, may be a name or ISO2.</summary>
    public string IdIssuerCountry { get; set; } = "";
    /// <summary>cl66 / cl31 — raw value, may be a name or ISO2.</summary>
    public string Nationality { get; set; } = "";
    /// <summary>cl68–cl73 / cl33–cl38 — address lines through postal code.</summary>
    public string[] AddressLines { get; set; } = Array.Empty<string>();
    /// <summary>cl73 / cl38 — also the last entry of <see cref="AddressLines"/>.</summary>
    public string PostalCode { get; set; } = "";

    public string FullName => $"{FirstName} {LastName}".Trim();

    /// <summary>DS_SBE field 14 — address lines joined with a single space.</summary>
    public string FullAddress => string.Join(" ",
        AddressLines.Where(a => !string.IsNullOrWhiteSpace(a)).Select(a => a.Trim()));
}
