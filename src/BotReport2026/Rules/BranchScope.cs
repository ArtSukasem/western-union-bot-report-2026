using BotReport2026.Models;

namespace BotReport2026.Rules;

/// <summary>
/// "Outbound at these branches only" — the scope Rules 202, 208 and 209 share (the online
/// channel by default). Kept in one place so the rules and the explain tab agree.
/// </summary>
public static class BranchScope
{
    /// <summary>A configured branch list, trimmed and uppercased for lookup. Empty = every branch.</summary>
    public static HashSet<string> Normalize(IEnumerable<string>? branches) =>
        (branches ?? Enumerable.Empty<string>())
            .Where(b => !string.IsNullOrWhiteSpace(b))
            .Select(b => b.Trim().ToUpperInvariant())
            .ToHashSet();

    public static bool IsOutboundAt(RspTransaction t, HashSet<string> branches) =>
        t.Direction == "OB" &&
        (branches.Count == 0 || branches.Contains(t.BranchAccountId.Trim().ToUpperInvariant()));

    /// <summary>For log and explain text: "ATH170025, ATH170014", or "ทั้งหมด" when empty.</summary>
    public static string Describe(HashSet<string> branches) =>
        branches.Count == 0 ? "ทั้งหมด" : string.Join(", ", branches);
}
