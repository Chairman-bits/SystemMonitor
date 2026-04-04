using System.Diagnostics;
using System.IO.Compression;

if (args.Length < 2)
{
    return;
}

var zipPath = args[0];
var appDir = args[1];

// 本体終了待ち
Thread.Sleep(2000);

// 一時展開先
var tempDir = Path.Combine(Path.GetTempPath(), "SystemMonitor_Update");

// 既存削除
if (Directory.Exists(tempDir))
{
    Directory.Delete(tempDir, true);
}

Directory.CreateDirectory(tempDir);

// まず一時フォルダに展開
ZipFile.ExtractToDirectory(zipPath, tempDir, true);

// zip直下にフォルダ1つだけなら、その中を更新元にする
var sourceRoot = tempDir;
var dirs = Directory.GetDirectories(tempDir);
var files = Directory.GetFiles(tempDir);

if (dirs.Length == 1 && files.Length == 0)
{
    sourceRoot = dirs[0];
}

// sourceRoot の中身を appDir に上書きコピー
foreach (var sourceFile in Directory.GetFiles(sourceRoot, "*", SearchOption.AllDirectories))
{
    var relativePath = Path.GetRelativePath(sourceRoot, sourceFile);
    var destFile = Path.Combine(appDir, relativePath);
    var destDir = Path.GetDirectoryName(destFile);

    if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
    {
        Directory.CreateDirectory(destDir);
    }

    File.Copy(sourceFile, destFile, true);
}

// 更新後に本体を再起動
var exePath = Path.Combine(appDir, "StealthStockOverlay.exe");
if (File.Exists(exePath))
{
    Process.Start(new ProcessStartInfo
    {
        FileName = exePath,
        UseShellExecute = true
    });
}