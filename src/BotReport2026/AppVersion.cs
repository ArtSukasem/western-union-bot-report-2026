namespace BotReport2026;

/// <summary>
/// The program version from the csproj &lt;Version&gt; ("0.3.0"), shown in the window
/// title and stamped into generated reports so a file can be traced to the build
/// that made it.
/// </summary>
public static class AppVersion
{
    public static string Text { get; } = Format(
        System.Reflection.Assembly.GetExecutingAssembly().GetName().Version);

    /// <summary>"BotReport 2026 v0.3.0"</summary>
    public static string Label => $"BotReport 2026 v{Text}";

    private static string Format(Version? v) =>
        v == null ? "?" : $"{v.Major}.{v.Minor}.{Math.Max(v.Build, 0)}";
}
