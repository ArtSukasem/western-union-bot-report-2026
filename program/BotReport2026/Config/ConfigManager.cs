using BotReport2026.Models;
using Newtonsoft.Json;

namespace BotReport2026.Config;

public static class ConfigManager
{
    private static readonly string ConfigPath =
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");

    // Without this, Newtonsoft.Json's default behavior is to append deserialized
    // list items onto AppConfig's field-initializer defaults (e.g. Rule203ExcludedBranches
    // starts as ["ATH170025"]) instead of replacing them — growing every list-typed
    // setting by its default's length on every single load.
    private static readonly JsonSerializerSettings Settings = new()
    {
        ObjectCreationHandling = ObjectCreationHandling.Replace
    };

    public static AppConfig Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                return JsonConvert.DeserializeObject<AppConfig>(json, Settings) ?? new AppConfig();
            }
        }
        catch { }
        return new AppConfig();
    }

    public static void Save(AppConfig config)
    {
        var json = JsonConvert.SerializeObject(config, Formatting.Indented);
        File.WriteAllText(ConfigPath, json);
    }
}
