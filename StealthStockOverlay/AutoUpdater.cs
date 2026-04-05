using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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
        client.DefaultRequestHeaders.CacheControl = new CacheControlHeaderValue
        {
            NoCache = true
        };
        return client;
    }

    public static async Task CheckSilentlyAsync()
    {
        try
        {
            using var client = CreateClient();
            var json = await client.GetStringAsync(VERSION_URL);

            _versionInfo = JsonSerializer.Deserialize<VersionInfo>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (_versionInfo == null)
            {
                HasUpdate = false;
                LatestVersionText = "";
                return;
            }

            _versionInfo.latest = NormalizeVersion(_versionInfo.latest);

            var currentVersion = ParseVersionSafe(GetCurrentVersion());
            var latestVersion = ParseVersionSafe(_versionInfo.latest);

            if (latestVersion > currentVersion)
            {
                HasUpdate = true;
                LatestVersionText = _versionInfo.latest;
            }
            else
            {
                HasUpdate = false;
                LatestVersionText = "";
            }
        }
        catch
        {
            HasUpdate = false;
            LatestVersionText = "";
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

            System.IO.Compression.ZipFile.ExtractToDirectory(tempZip, extractDir, true);

            var updaterPath = Path.Combine(extractDir, "Updater.exe");
            var appDir = AppContext.BaseDirectory.TrimEnd('\\');

            if (!File.Exists(updaterPath))
            {
                System.Windows.MessageBox.Show(
                    "app.zip 内に Updater.exe が見つかりません。",
                    "アップデートエラー",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
                return;
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = updaterPath,
                Arguments = $"\"{tempZip}\" \"{appDir}\"",
                UseShellExecute = true,
                WorkingDirectory = extractDir
            });

            Environment.Exit(0);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                "アップデートの開始に失敗しました。\n" + ex.Message,
                "アップデート",
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

            var sb = new StringBuilder();

            foreach (var item in _releaseNotes)
            {
                sb.AppendLine($"■ {item.version} ({item.publishedAt})");

                if (item.notes != null && item.notes.Count > 0)
                {
                    foreach (var note in item.notes)
                    {
                        sb.AppendLine($"・{note}");
                    }
                }
                else
                {
                    sb.AppendLine("・更新内容なし");
                }

                sb.AppendLine();
            }

            System.Windows.MessageBox.Show(
                sb.ToString().Trim(),
                "更新履歴",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
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
}