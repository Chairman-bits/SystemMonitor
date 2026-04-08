using System.Collections.Generic;

namespace StealthStockOverlay.Models
{
    public class AppSettings
    {
        public List<string> Symbols { get; set; } = new();
        public int RefreshSeconds { get; set; } = 5;

        public bool HotkeyCtrl { get; set; }
        public bool HotkeyShift { get; set; }
        public bool HotkeyAlt { get; set; } = true;
        public bool HotkeyWin { get; set; }

        public string HotkeyKey { get; set; } = "Z";

        public bool ShowOnlyWhileHotkeyHeld { get; set; } = true;

        public string OverlayPosition { get; set; } = "BottomRight";

        public int OverlayMarginX { get; set; } = 12;
        public int OverlayMarginY { get; set; } = 12;

        public int DisplayMonitorIndex { get; set; } = 0;
    }
}