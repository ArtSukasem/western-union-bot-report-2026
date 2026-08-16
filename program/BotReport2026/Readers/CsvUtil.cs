using System.Text;

namespace BotReport2026.Readers;

/// <summary>
/// Minimal RFC 4180 CSV reader. RSP input files are plain CSV (UTF-8, one header
/// row), so no external CSV package is pulled in for this.
/// </summary>
public static class CsvUtil
{
    /// <summary>
    /// Streams the file one row at a time — RSP outbound files run to ~20 MB, so
    /// nothing is buffered beyond the current row.
    /// </summary>
    public static IEnumerable<string[]> ReadRows(string path)
    {
        using var reader = OpenReader(path);

        var fields = new List<string>();
        var field = new StringBuilder();
        bool inQuotes = false;
        bool rowHasContent = false;

        int read;
        while ((read = reader.Read()) >= 0)
        {
            char c = (char)read;

            if (inQuotes)
            {
                if (c == '"')
                {
                    // Doubled quote inside a quoted field is a literal quote
                    if (reader.Peek() == '"') { field.Append('"'); reader.Read(); }
                    else inQuotes = false;
                }
                else field.Append(c);
                continue;
            }

            switch (c)
            {
                case '"':
                    inQuotes = true;
                    rowHasContent = true;
                    break;

                case ',':
                    fields.Add(field.ToString());
                    field.Clear();
                    rowHasContent = true;
                    break;

                case '\r':
                    break; // CRLF — the LF ends the row

                case '\n':
                    fields.Add(field.ToString());
                    field.Clear();
                    yield return fields.ToArray();
                    fields.Clear();
                    rowHasContent = false;
                    break;

                default:
                    field.Append(c);
                    rowHasContent = true;
                    break;
            }
        }

        // Last row when the file does not end with a newline
        if (rowHasContent || field.Length > 0)
        {
            fields.Add(field.ToString());
            yield return fields.ToArray();
        }
    }

    /// <summary>Counts data rows (excluding header rows) without materialising them.</summary>
    public static int CountDataRows(string path, int headerRows)
    {
        try
        {
            int total = 0;
            foreach (var _ in ReadRows(path)) total++;
            return Math.Max(0, total - headerRows);
        }
        catch { return 0; }
    }

    /// <summary>
    /// Normalises one raw field: trims surrounding whitespace (dates arrive as
    /// " 01/06/2026") and unwraps the Excel-escape form ="0812345678" that the
    /// export uses to stop Excel eating leading zeros.
    /// </summary>
    public static string Clean(string? raw)
    {
        if (string.IsNullOrEmpty(raw)) return "";
        string s = raw.Trim();
        if (s.Length >= 3 && s.StartsWith("=\"", StringComparison.Ordinal) && s.EndsWith("\"", StringComparison.Ordinal))
            s = s.Substring(2, s.Length - 3).Trim();
        return s;
    }

    private static StreamReader OpenReader(string path) =>
        new(path, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            detectEncodingFromByteOrderMarks: true);
}
