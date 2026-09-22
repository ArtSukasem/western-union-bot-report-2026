using System.Text;

namespace BotReport2026.Writers;

/// <summary>
/// Shared CSV writing for the ธปท. submission files.
/// </summary>
public static class CsvFile
{
    /// <summary>
    /// UTF-8 **with** BOM. Without it Excel on a Thai Windows opens the file as
    /// CP874/ANSI and every Thai character turns to garbage. The BOM is what makes
    /// a double-clicked CSV readable, and UTF-8 readers ignore it.
    /// </summary>
    private static readonly Encoding Utf8WithBom = new UTF8Encoding(true);

    /// <summary>Field separator: pipe, not comma.</summary>
    public const string Delimiter = "|";

    public static void Write(IEnumerable<string[]> rows, string path)
    {
        using var writer = new StreamWriter(path, false, Utf8WithBom);
        foreach (var row in rows)
            writer.WriteLine(string.Join(Delimiter, row.Select(Escape)));
    }

    /// <summary>
    /// Every field is wrapped in double quotes, unconditionally — the submission
    /// format wants uniform quoting rather than quote-only-when-needed, so a field
    /// that happens to contain a pipe, a line break or nothing at all all look the
    /// same to the reader. Embedded quotes are doubled, as in RFC 4180.
    /// </summary>
    public static string Escape(string? value) =>
        "\"" + (value ?? "").Replace("\"", "\"\"") + "\"";
}
