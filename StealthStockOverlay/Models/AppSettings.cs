using System.Collections.Generic;

namespace StealthStockOverlay
{
    public class AppSettings
    {
        public List<string> Symbols { get; set; } = new();
        public int RefreshSeconds { get; set; } = 5;

        public bool HotkeyCtrl { get; set; }
        public bool HotkeyShift { get; set; }
        public bool HotkeyAlt { get; set; } = true;
        public bool HotkeyWin { get; set; }

        public string HotkeyKey { get; set; } = "Q";

        public int OverlayMarginX { get; set; } = 12;
        public int OverlayMarginY { get; set; } = 12;

        public string OverlayPosition { get; set; } = "右下";

        // 新方式
        public bool ShowOnlyWhileHeld { get; set; } = true;
        public string DisplayMonitor { get; set; } = string.Empty;

        // 旧コード互換
        public bool ShowOnlyWhileHotkeyHeld
        {
            get => ShowOnlyWhileHeld;
            set => ShowOnlyWhileHeld = value;
        }

        public int DisplayMonitorIndex { get; set; } = 0;
    }
}