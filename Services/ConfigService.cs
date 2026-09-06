using System.IO;
using System.Text.Json;
using GameBotMonitor.Models;

namespace GameBotMonitor.Services;

public static class ConfigService
{
    private static readonly string ConfigFilePath = Path.Combine(AppContext.BaseDirectory, "config.json");
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static AppConfig Load()
    {
        try
        {
            if (File.Exists(ConfigFilePath))
            {
                var json = File.ReadAllText(ConfigFilePath);
                var config = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions);
                if (config != null) return config;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ConfigService] Error loading config: {ex.Message}");
        }

        var defaultConfig = new AppConfig();
        Save(defaultConfig);
        return defaultConfig;
    }

    public static void Save(AppConfig config)
    {
        try
        {
            var json = JsonSerializer.Serialize(config, JsonOptions);
            File.WriteAllText(ConfigFilePath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ConfigService] Error saving config: {ex.Message}");
        }
    }
}
