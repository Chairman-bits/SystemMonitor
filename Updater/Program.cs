using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Threading;

namespace Updater
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var logPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SystemMonitorHelper",
                "updater.log");

            void Log(string message)
            {
                var dir = Path.GetDirectoryName(logPath);
                if (!string.IsNullOrEmpty(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                File.AppendAllText(
                    logPath,
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}");
            }

            try
            {
                Log("Updater start");

                if (args.Length < 2)
                {
                    Log("args不足");
                    return;
                }

                var zipPath = args[0].Trim().Trim('"');
                var appDir = args[1].Trim().Trim('"');

                Log($"zipPath = {zipPath}");
                Log($"appDir = {appDir}");

                Thread.Sleep(2000);

                var tempDir = Path.Combine(
                    Path.GetTempPath(),
                    "SystemMonitor_Apply_" + Guid.NewGuid().ToString("N"));

                Directory.CreateDirectory(tempDir);
                Log($"tempDir = {tempDir}");

                ZipFile.ExtractToDirectory(zipPath, tempDir, true);
                Log("zip展開完了");

                var sourceExe = Path.Combine(tempDir, "SystemMonitorHelper.exe");
                var destExe = Path.Combine(appDir, "SystemMonitorHelper.exe");
                var oldUpdater = Path.Combine(appDir, "Updater.exe");

                if (!File.Exists(sourceExe))
                {
                    Log("zip内に SystemMonitorHelper.exe が見つかりません");
                    return;
                }

                // 旧 Updater.exe が残っていたら消す
                if (File.Exists(oldUpdater))
                {
                    try
                    {
                        File.Delete(oldUpdater);
                        Log("旧 Updater.exe 削除完了");
                    }
                    catch (Exception ex)
                    {
                        Log($"旧 Updater.exe 削除失敗: {ex.Message}");
                    }
                }

                // exe 上書き用に少し待つ
                Thread.Sleep(1000);

                try
                {
                    if (File.Exists(destExe))
                    {
                        File.Delete(destExe);
                        Log("旧 SystemMonitorHelper.exe 削除完了");
                    }
                }
                catch (Exception ex)
                {
                    Log($"旧 exe 削除失敗: {ex.Message}");
                }

                File.Copy(sourceExe, destExe, true);
                Log("新 exe コピー完了");

                if (File.Exists(destExe))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = destExe,
                        UseShellExecute = true,
                        WorkingDirectory = appDir
                    });

                    Log("再起動成功");
                }
                else
                {
                    Log("更新後 exe が見つかりません");
                }

                try
                {
                    File.Delete(zipPath);
                    Log("zip削除完了");
                }
                catch (Exception ex)
                {
                    Log($"zip削除失敗: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                File.AppendAllText(
                    logPath,
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ERROR {ex}{Environment.NewLine}");
            }
        }
    }
}