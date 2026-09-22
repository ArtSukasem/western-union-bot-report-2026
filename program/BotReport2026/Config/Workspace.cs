using Newtonsoft.Json;

namespace BotReport2026.Config;

/// <summary>
/// The working folder every input, lookup and result path is resolved under, plus the
/// fixed sub-folder name each job uses inside it. The user picks the root from the bar
/// at the top of MainForm; everything below it keeps the same layout the repo has always
/// used, so a working folder is portable — copy it to another machine and point the app
/// at it.
///
/// Persisted in its own workspace.json beside the executable rather than in config.json:
/// TabSettings.GetConfig() rebuilds AppConfig from its controls on every save, which
/// would silently drop a path property it does not know about.
/// </summary>
public static class Workspace
{
    // Sub-folder ("job") names inside the working folder.
    public const string RspInboundFolder = "input-rsp-inbound";
    public const string RspOutboundFolder = "input-rsp-outbound";
    public const string TxnReportFolder = "input-transaction-report";
    public const string LookupsFolder = "lookups";
    public const string MapperFolder = "mapper";
    public const string ReferenceFolder = "SAE-lookups";
    public const string ResultsFolder = "report-results";

    /// <summary>Folder created beside the executable when no working folder has been
    /// picked and the app is not running from a repo checkout.</summary>
    public const string DefaultFolderName = "bot-report-files";

    private static readonly string SettingsPath =
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "workspace.json");

    private static string _root = ResolveInitialRoot();

    /// <summary>Absolute path of the working folder. Change it through <see cref="SetRoot"/>.</summary>
    public static string Root => _root;

    public static string InputRspInboundDir => Path.Combine(_root, RspInboundFolder);
    public static string InputRspOutboundDir => Path.Combine(_root, RspOutboundFolder);
    public static string InputTxnReportDir => Path.Combine(_root, TxnReportFolder);
    public static string LookupsDir => Path.Combine(_root, LookupsFolder);
    public static string MapperDir => Path.Combine(_root, MapperFolder);

    /// <summary>DS_SBE/DS_SAE reference tables, produced by tools/extract_v3_lookups.py.</summary>
    public static string ReferenceDataDirectory => Path.Combine(_root, ReferenceFolder);
    public static string OutputDirectory => Path.Combine(_root, ResultsFolder);

    public static string SanctionListPath => Path.Combine(LookupsDir, "saction_list.xlsx");
    public static string CrimeListPath =>
        Path.Combine(LookupsDir, "financial_crime_individuals_suspicious_counter_customer_behavior.xlsx");
    public static string OccupationMapPath =>
        Path.Combine(MapperDir, "list_of_occupation_expected_monthly_income.xlsx");

    /// <summary>Every folder the app reads from or writes to, in display order.</summary>
    public static IEnumerable<string> AllFolders => new[]
    {
        InputRspInboundDir, InputRspOutboundDir, InputTxnReportDir,
        LookupsDir, MapperDir, ReferenceDataDirectory, OutputDirectory
    };

    /// <summary>Creates the job sub-folders so users have somewhere to drop files.</summary>
    public static void EnsureFolders()
    {
        foreach (var dir in AllFolders)
        {
            try { Directory.CreateDirectory(dir); } catch { }
        }
    }

    /// <summary>
    /// Points the app at <paramref name="root"/>, creates its sub-folders and remembers
    /// the choice. Throws if the path is unusable, so the caller can show the reason.
    /// </summary>
    public static void SetRoot(string root)
    {
        if (string.IsNullOrWhiteSpace(root))
            throw new ArgumentException("ยังไม่ได้เลือกโฟลเดอร์", nameof(root));

        string full = Path.GetFullPath(root);
        Directory.CreateDirectory(full);   // throws with a real reason if the path is bad
        _root = full;
        EnsureFolders();
        SaveRoot(full);
    }

    /// <summary>
    /// Copies reference files (lookups, mapper, SAE-lookups) from <paramref name="fromRoot"/>
    /// into the current working folder, skipping any that already exist there. Returns the
    /// number copied. Used when the user moves to a fresh working folder — those tables are
    /// per-install data, not per-month, so a new folder would otherwise start empty.
    /// </summary>
    public static int CopyReferenceFiles(string fromRoot)
    {
        int copied = 0;
        foreach (var folder in new[] { LookupsFolder, MapperFolder, ReferenceFolder })
        {
            string src = Path.Combine(fromRoot, folder);
            string dst = Path.Combine(_root, folder);
            if (!Directory.Exists(src)) continue;
            if (string.Equals(Path.GetFullPath(src), Path.GetFullPath(dst),
                    StringComparison.OrdinalIgnoreCase)) continue;

            Directory.CreateDirectory(dst);
            foreach (var file in Directory.GetFiles(src))
            {
                string target = Path.Combine(dst, Path.GetFileName(file));
                if (File.Exists(target)) continue;
                try { File.Copy(file, target); copied++; } catch { }
            }
        }
        return copied;
    }

    /// <summary>
    /// True when the working folder holds none of the reference files a run needs, so the
    /// caller can offer to copy them across instead of letting the run fail half-way.
    /// </summary>
    public static bool IsMissingReferenceFiles() => !HasReferenceFiles(_root);

    /// <summary>True when <paramref name="root"/> holds at least one reference file — i.e.
    /// there is something worth copying out of it.</summary>
    public static bool HasReferenceFiles(string root)
    {
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root)) return false;

        string saeDir = Path.Combine(root, ReferenceFolder);
        return File.Exists(Path.Combine(root, LookupsFolder, Path.GetFileName(SanctionListPath))) ||
               File.Exists(Path.Combine(root, LookupsFolder, Path.GetFileName(CrimeListPath))) ||
               File.Exists(Path.Combine(root, MapperFolder, Path.GetFileName(OccupationMapPath))) ||
               (Directory.Exists(saeDir) && Directory.GetFiles(saeDir, "*.xlsx").Length > 0);
    }

    // --- persistence ---

    private class WorkspaceSettings
    {
        public string Root { get; set; } = "";
    }

    private static void SaveRoot(string root)
    {
        try
        {
            File.WriteAllText(SettingsPath,
                JsonConvert.SerializeObject(new WorkspaceSettings { Root = root }, Formatting.Indented));
        }
        catch { }
    }

    /// <summary>
    /// Saved folder wins; otherwise the repo checkout the executable was built inside
    /// (so a dev build keeps using the repo's folders as before); otherwise a
    /// <see cref="DefaultFolderName"/> folder beside the executable.
    /// </summary>
    private static string ResolveInitialRoot()
    {
        string? saved = LoadSavedRoot();
        if (saved != null) return saved;

        string? repoRoot = FindRepoRoot();
        if (repoRoot != null) return repoRoot;

        return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, DefaultFolderName);
    }

    private static string? LoadSavedRoot()
    {
        try
        {
            if (!File.Exists(SettingsPath)) return null;
            var s = JsonConvert.DeserializeObject<WorkspaceSettings>(File.ReadAllText(SettingsPath));
            // A folder that has since been deleted or sits on a disconnected drive falls
            // back to the default instead of leaving every path pointing at nothing.
            if (s != null && !string.IsNullOrWhiteSpace(s.Root) && Directory.Exists(s.Root))
                return Path.GetFullPath(s.Root);
        }
        catch { }
        return null;
    }

    /// <summary>
    /// Walks up from the build output looking for a folder that already holds the job
    /// sub-folders — i.e. the repo checkout when running from bin\Debug\net8.0-windows\.
    /// </summary>
    private static string? FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
        for (int i = 0; i < 6 && dir != null; i++, dir = dir.Parent)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, RspInboundFolder)) ||
                Directory.Exists(Path.Combine(dir.FullName, LookupsFolder)))
                return dir.FullName;
        }
        return null;
    }
}
