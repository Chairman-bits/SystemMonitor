using System;
using System.Diagnostics;
using System.IO;
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

                var sourceExe = args[0].Trim('"');
                var destExe = args[1].Trim('"');
                var appDir = Path.GetDirectoryName(destExe) ?? "";

                Log($"sourceExe = {sourceExe}");
                Log($"destExe = {destExe}");

                if (!File.Exists(sourceExe))
                {
                    Log("新しいexeが見つかりません");
                    return;
                }

                Thread.Sleep(3000);

                var copied = false;

                for (int i = 0; i < 15; i++)
                {
                    try
                    {
                        if (File.Exists(destExe))
                        {
                            File.Delete(destExe);
                            Log("旧 exe 削除完了");
                        }

                        File.Copy(sourceExe, destExe, true);
                        Log("新 exe コピー完了");
                        copied = true;
                        break;
                    }
                    catch (Exception ex)
                    {
                        Log($"コピー失敗 {i + 1}/15: {ex.Message}");
                        Thread.Sleep(1000);
                    }
                }

                if (!copied)
                {
                    Log("コピー失敗のため終了");
                    return;
                }

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