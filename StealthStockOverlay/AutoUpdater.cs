using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace StealthStockOverlay;

public static class AutoUpdater
{
    private const string VERSION_URL = "https://raw.githubusercontent.com/Chairman-bits/SystemMonitor/main/version.json";

    private static string LogPath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SystemMonitorHelper",
            "autoupdate.log");

    private static void Log(string message)
    {
        var dir = Path.GetDirectoryName(LogPath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        File.AppendAllText(
            LogPath,
            $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}");
    }

    private static string NormalizeVersionText(string? text)
    {
        var raw = (text ?? "")
            .Replace("\uFEFF", "")
            .Replace("\r", "")
            .Replace("\n", "")
            .Trim();

        if (Version.TryParse(raw, out var version))
        {
            // 1.1.1.0 → 1.1.1 に寄せる
            if (version.Revision == 0)
            {
                return $"{version.Major}.{version.Minor}.{version.Build}";
            }

            return version.ToString();
        }

        return raw;
    }

    private static string GetCurrentVersion()
    {
        var exePath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(exePath) || !File.Exists(exePath))
        {
            return "0.0.0";
        }

        var info = FileVersionInfo.GetVersionInfo(exePath);

        // FileVersion を優先
        var fileVersion = NormalizeVersionText(info.FileVersion);
        if (!string.IsNullOrWhiteSpace(fileVersion))
        {
            return fileVersion;
        }

        return "0.0.0";
    }

    public static async Task CheckAsync()
    {
        try
        {
            Log("CheckAsync start");

            using var client = new HttpClient();
            var json = await client.GetStringAsync(VERSION_URL);
            Log("version.json 取得成功");

            var info = JsonSerializer.Deserialize<UpdateInfo>(json);
            if (info == null)
            {
                Log("version.json deserialize失敗");
                return;
            }

            var serverVersion = NormalizeVersionText(info.version);
            var currentVersion = GetCurrentVersion();

            Log($"server version = {serverVersion}");
            Log($"current version = {currentVersion}");

            if (serverVersion == currentVersion)
            {
                Log("更新不要");
                return;
            }

            var result = System.Windows.MessageBox.Show(
                $"新しいバージョン {serverVersion} があります。更新しますか？",
                "アップデート",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Information);

            if (result != System.Windows.MessageBoxResult.Yes)
            {
                Log("ユーザーが更新キャンセル");
                return;
            }

            var tempRoot = Path.Combine(
                Path.GetTempPath(),
                "SystemMonitorUpdate_" + Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(tempRoot);

            var tempZip = Path.Combine(tempRoot, "app.zip");
            var extractDir = Path.Combine(tempRoot, "extract");
            Directory.CreateDirectory(extractDir);

            var data = await client.GetByteArrayAsync(info.url);
            await File.WriteAllBytesAsync(tempZip, data);
            Log($"zip 保存成功: {tempZip}");

            ZipFile.ExtractToDirectory(tempZip, extractDir, true);
            Log($"zip 展開成功: {extractDir}");

            var updaterPath = Path.Combine(extractDir, "Updater.exe");
            var appDir = AppContext.BaseDirectory.TrimEnd('\\');

            if (!File.Exists(updaterPath))
            {
                Log("展開先に Updater.exe が見つかりません");
                return;
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = updaterPath,
                Arguments = $"\"{tempZip}\" \"{appDir}\"",
                UseShellExecute = true,
                WorkingDirectory = extractDir
            });

            Log("一時フォルダの Updater.exe 起動成功");
            Environment.Exit(0);
        }
        catch (Exception ex)
        {
            Log("ERROR: " + ex);
        }
    }

    private class UpdateInfo
    {
        public string version { get; set; } = "";
        public string url { get; set; } = "";
    }
}