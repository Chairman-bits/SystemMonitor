namespace StealthStockOverlay.Models;

public class AppSettings
{
    public List<string> Symbols { get; set; } = new()
    {
        "4063",
        "8001",
        "8058",
        "9432"
    };

    public int RefreshSeconds { get; set; } = 10;

    public bool HotkeyCtrl { get; set; } = true;
    public bool HotkeyShift { get; set; } = true;
    public bool HotkeyAlt { get; set; } = false;
    public bool HotkeyWin { get; set; } = false;
    public string HotkeyKey { get; set; } = "Z";

    public bool ShowOnlyWhileHotkeyHeld { get; set; } = true;

    public string OverlayPosition { get; set; } = "BottomRight";
    public double OverlayMarginX { get; set; } = 12;
    public double OverlayMarginY { get; set; } = 12;

    // 追加: 表示するディスプレイ番号
    public int DisplayMonitorIndex { get; set; } = 0;
}
