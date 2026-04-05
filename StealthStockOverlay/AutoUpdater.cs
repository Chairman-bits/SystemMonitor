using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace StealthStockOverlay;

public static class AutoUpdater
{
    private const string VERSION_URL = "https://raw.githubusercontent.com/Chairman-bits/SystemMonitor/main/version.json";
    private const string RELEASE_NOTES_URL = "https://raw.githubusercontent.com/Chairman-bits/SystemMonitor/main/release-notes.json";

    private static VersionInfo? _versionInfo;
    private static List<ReleaseNote>? _releaseNotes;

    public static bool HasUpdate { get; private set; }
    public static string LatestVersionText { get; private set; } = "";

    private static string NormalizeVersion(string? versionText)
    {
        var text = (versionText ?? string.Empty)
            .Replace("\uFEFF", "")
            .Trim();

        if (string.IsNullOrWhiteSpace(text))
        {
            return "0.0.0";
        }

        if (Version.TryParse(text, out var version))
        {
            if (version.Revision == 0)
            {
                return $"{version.Major}.{version.Minor}.{version.Build}";
            }

            return version.ToString();
        }

        return "0.0.0";
    }

    private static Version ParseVersionSafe(string? versionText)
    {
        var normalized = NormalizeVersion(versionText);

        if (Version.TryParse(normalized, out var version))
        {
            return version;
        }

        return new Version(0, 0, 0);
    }

    private static string GetCurrentVersion()
    {
        var exePath = Environment.ProcessPath;

        if (string.IsNullOrWhiteSpace(exePath) || !File.Exists(exePath))
        {
            return "0.0.0";
        }

        var info = FileVersionInfo.GetVersionInfo(exePath);
        return NormalizeVersion(info.FileVersion);
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient();
        client.Timeout = TimeSpan.FromSeconds(15);
        client.DefaultRequestHeaders.CacheControl = new CacheControlHeaderValue
        {
            NoCache = true
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("SystemMonitorHelper/1.0");
        return client;
    }

    public static async Task CheckSilentlyAsync()
    {
        try
        {
            LastErrorMessage = "";

            using var client = CreateClient();
            var json = await client.GetStringAsync(VERSION_URL);

            _versionInfo = JsonSerializer.Deserialize<VersionInfo>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            CurrentVersionText = GetCurrentVersion();

            if (_versionInfo == null)
            {
                LastCheckSucceeded = false;
                HasUpdate = false;
                LatestVersionText = "";
                LastErrorMessage = "version.json の解析結果が null でした。";
                return;
            }

            _versionInfo.latest = NormalizeVersion(_versionInfo.latest);
            LatestVersionText = _versionInfo.latest;

            var currentVersion = ParseVersionSafe(CurrentVersionText);
            var latestVersion = ParseVersionSafe(LatestVersionText);

            if (latestVersion <= new Version(0, 0, 0))
            {
                LastCheckSucceeded = false;
                HasUpdate = false;
                LastErrorMessage = $"latest の値が不正です: {LatestVersionText}";
                return;
            }

            LastCheckSucceeded = true;
            HasUpdate = latestVersion > currentVersion;
        }
        catch (Exception ex)
        {
            LastCheckSucceeded = false;
            HasUpdate = false;
            CurrentVersionText = GetCurrentVersion();
            LatestVersionText = "";
            LastErrorMessage = ex.Message;
        }
    }

    public static async Task CheckAsync()
    {
        await CheckSilentlyAsync();
    }

    public static async Task ApplyUpdateAsync()
    {
        if (_versionInfo == null)
        {
            await CheckSilentlyAsync();
        }

        if (_versionInfo == null || string.IsNullOrWhiteSpace(_versionInfo.url))
        {
            System.Windows.MessageBox.Show(
                "更新情報の取得に失敗しました。",
                "アップデート",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
            return;
        }

        try
        {
            var tempRoot = Path.Combine(
                Path.GetTempPath(),
                "SystemMonitorUpdate_" + Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(tempRoot);

            var tempZip = Path.Combine(tempRoot, "app.zip");
            var extractDir = Path.Combine(tempRoot, "extract");
            Directory.CreateDirectory(extractDir);

            using (var client = CreateClient())
            {
                var data = await client.GetByteArrayAsync(_versionInfo.url);
                await File.WriteAllBytesAsync(tempZip, data);
            }

            ZipFile.ExtractToDirectory(tempZip, extractDir, true);

            var updaterPath = Path.Combine(extractDir, "Updater.exe");
            var newExePath = Path.Combine(extractDir, "SystemMonitorHelper.exe");
            var appDir = AppContext.BaseDirectory.TrimEnd('\\');
            var currentExePath = Path.Combine(appDir, "SystemMonitorHelper.exe");

            if (!File.Exists(updaterPath))
            {
                System.Windows.MessageBox.Show(
                    "app.zip 内に Updater.exe が見つかりません。",
                    "アップデートエラー",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
                return;
            }

            if (!File.Exists(newExePath))
            {
                System.Windows.MessageBox.Show(
                    "app.zip 内に SystemMonitorHelper.exe が見つかりません。",
                    "アップデートエラー",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
                return;
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = updaterPath,
                Arguments = $"\"{newExePath}\" \"{currentExePath}\"",
                UseShellExecute = true,
                WorkingDirectory = extractDir
            });

            Environment.Exit(0);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                "アップデートの開始に失敗しました。\n" + ex.Message,
                "アップデートエラー",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
    }

    public static async Task ShowReleaseNotesAsync()
    {
        try
        {
            if (_releaseNotes == null)
            {
                using var client = CreateClient();
                var json = await client.GetStringAsync(RELEASE_NOTES_URL);

                _releaseNotes = JsonSerializer.Deserialize<List<ReleaseNote>>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }) ?? new List<ReleaseNote>();
            }

            if (_releaseNotes.Count == 0)
            {
                System.Windows.MessageBox.Show(
                    "更新履歴はありません。",
                    "更新履歴",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
                return;
            }

            var window = new StealthStockOverlay.Windows.UpdateHistoryWindow(_releaseNotes);
            window.ShowDialog();
        }
        catch
        {
            System.Windows.MessageBox.Show(
                "更新履歴の取得に失敗しました。",
                "更新履歴",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
        }
    }

    public class VersionInfo
    {
        public string latest { get; set; } = "";
        public string url { get; set; } = "";
    }

    public class ReleaseNote
    {
        public string version { get; set; } = "";
        public string publishedAt { get; set; } = "";
        public List<string> notes { get; set; } = new();
    }

    public static bool LastCheckSucceeded { get; private set; }
    public static string CurrentVersionText { get; private set; } = "";

    public static string LastErrorMessage { get; private set; } = "";
}