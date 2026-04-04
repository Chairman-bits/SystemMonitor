using System.IO;
using System.Text.Json;
using StealthStockOverlay.Models;

namespace StealthStockOverlay.Services;

public static class SettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static string GetSettingsDirectory()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "StealthStockOverlay");

        Directory.CreateDirectory(dir);
        return dir;
    }

    public static string GetSettingsPath()
    {
        return Path.Combine(GetSettingsDirectory(), "settings.json");
    }

    public static AppSettings Load()
    {
        var path = GetSettingsPath();

        if (!File.Exists(path))
        {
            var defaults = new AppSettings();
            Save(defaults);
            return defaults;
        }

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

    public static void Save(AppSettings settings)
    {
        var path = GetSettingsPath();
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(path, json);
    }
}
