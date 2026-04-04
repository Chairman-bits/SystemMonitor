using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;

namespace StealthStockOverlay;

public static class AutoUpdater
{
    private const string VERSION_URL =
    "https://raw.githubusercontent.com/Chairman-bits/SystemMonitor/main/version.json";

    public static async Task CheckAsync()
    {
        try
        {
            var client = new HttpClient();
            var json = await client.GetStringAsync(VERSION_URL);
            var info = JsonSerializer.Deserialize<UpdateInfo>(json);

            var versionFile = Path.Combine(AppContext.BaseDirectory, "version.txt");
            if (!File.Exists(versionFile))
            {
                return;
            }

            var currentVersion = File.ReadAllText(versionFile).Trim();

            if (info == null || info.version == currentVersion)
            {
                return;
            }

            var result = System.Windows.MessageBox.Show(
                $"新しいバージョン {info.version} があります。更新しますか？",
                "アップデート",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Information);

            if (result != System.Windows.MessageBoxResult.Yes)
            {
                return;
            }

            var tempZip = Path.Combine(Path.GetTempPath(), "update.zip");

            var data = await client.GetByteArrayAsync(info.url);
            await File.WriteAllBytesAsync(tempZip, data);

            var updaterPath = Path.Combine(AppContext.BaseDirectory, "Updater.exe");
            if (!File.Exists(updaterPath))
            {
                System.Windows.MessageBox.Show(
                    "Updater.exe が見つかりません。",
                    "アップデートエラー",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
                return;
            }

            Process.Start(
                updaterPath,
                $"\"{tempZip}\" \"{AppContext.BaseDirectory}\"");

            Environment.Exit(0);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"アップデート確認中にエラーが発生しました。\\n{ex.Message}",
                "アップデートエラー",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
        }
    }

    private class UpdateInfo
    {
        public string version { get; set; } = "";
        public string url { get; set; } = "";
    }
}