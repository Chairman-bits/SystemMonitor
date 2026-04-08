using StealthStockOverlay.Models;
using System;
using System.Linq;
using System.Windows.Controls;
using WinForms = System.Windows.Forms;

namespace StealthStockOverlay
{
    public partial class SettingsWindow : System.Windows.Window
    {
        public AppSettings? ResultSettings { get; private set; }

        public SettingsWindow(AppSettings settings)
        {
            InitializeComponent();

            SymbolsTextBox.Text = string.Join(Environment.NewLine, settings.Symbols);
            RefreshSecondsTextBox.Text = settings.RefreshSeconds.ToString();

            CtrlCheckBox.IsChecked = settings.HotkeyCtrl;
            ShiftCheckBox.IsChecked = settings.HotkeyShift;
            AltCheckBox.IsChecked = settings.HotkeyAlt;
            WinCheckBox.IsChecked = settings.HotkeyWin;
            ShowOnlyWhileHeldCheckBox.IsChecked = settings.ShowOnlyWhileHotkeyHeld;

            for (char c = 'A'; c <= 'Z'; c++)
            {
                HotkeyKeyComboBox.Items.Add(c.ToString());
            }

            for (char c = '0'; c <= '9'; c++)
            {
                HotkeyKeyComboBox.Items.Add(c.ToString());
            }

            HotkeyKeyComboBox.SelectedItem = string.IsNullOrWhiteSpace(settings.HotkeyKey)
                ? "Z"
                : settings.HotkeyKey.ToUpperInvariant();

            OverlayMarginXTextBox.Text = settings.OverlayMarginX.ToString();
            OverlayMarginYTextBox.Text = settings.OverlayMarginY.ToString();

            foreach (var item in OverlayPositionComboBox.Items.OfType<ComboBoxItem>())
            {
                if ((item.Tag?.ToString() ?? "") == settings.OverlayPosition)
                {
                    OverlayPositionComboBox.SelectedItem = item;
                    break;
                }
            }

            if (OverlayPositionComboBox.SelectedItem is null)
            {
                OverlayPositionComboBox.SelectedIndex = 0;
            }

            LoadDisplayMonitors(settings.DisplayMonitorIndex);
        }

        private void LoadDisplayMonitors(int selectedIndex)
        {
            DisplayMonitorComboBox.Items.Clear();

            var screens = WinForms.Screen.AllScreens;

            for (int i = 0; i < screens.Length; i++)
            {
                var screen = screens[i];
                var label = $"ディスプレイ {i + 1} ({screen.Bounds.Width}x{screen.Bounds.Height})";

                if (screen.Primary)
                {
                    label += " [メイン]";
                }

                DisplayMonitorComboBox.Items.Add(new ComboBoxItem
                {
                    Content = label,
                    Tag = i
                });
            }

            if (DisplayMonitorComboBox.Items.Count > 0)
            {
                var safeIndex = Math.Clamp(selectedIndex, 0, DisplayMonitorComboBox.Items.Count - 1);
                DisplayMonitorComboBox.SelectedIndex = safeIndex;
            }
        }

        private void Save_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            var symbols = SymbolsTextBox.Text
                .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(10)
                .ToList();

            if (symbols.Count == 0)
            {
                System.Windows.MessageBox.Show(
                    "銘柄を1つ以上入力してください。",
                    "入力エラー",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
                return;
            }

            if (!int.TryParse(RefreshSecondsTextBox.Text.Trim(), out var refreshSeconds) ||
                refreshSeconds < 3 || refreshSeconds > 300)
            {
                System.Windows.MessageBox.Show(
                    "更新間隔は 3〜300 秒で入力してください。",
                    "入力エラー",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
                return;
            }

            var hotkeyKey = (HotkeyKeyComboBox.SelectedItem?.ToString() ?? "").Trim().ToUpperInvariant();

            if (string.IsNullOrWhiteSpace(hotkeyKey))
            {
                System.Windows.MessageBox.Show(
                    "ホットキーのキーを選択してください。",
                    "入力エラー",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
                return;
            }

            if (CtrlCheckBox.IsChecked != true &&
                ShiftCheckBox.IsChecked != true &&
                AltCheckBox.IsChecked != true &&
                WinCheckBox.IsChecked != true)
            {
                System.Windows.MessageBox.Show(
                    "ホットキーは修飾キーを1つ以上選択してください。",
                    "入力エラー",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
                return;
            }

            if (!double.TryParse(OverlayMarginXTextBox.Text.Trim(), out var marginX) ||
                marginX < 0 || marginX > 200)
            {
                System.Windows.MessageBox.Show(
                    "横余白は 0〜200 で入力してください。",
                    "入力エラー",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
                return;
            }

            if (!double.TryParse(OverlayMarginYTextBox.Text.Trim(), out var marginY) ||
                marginY < 0 || marginY > 200)
            {
                System.Windows.MessageBox.Show(
                    "縦余白は 0〜200 で入力してください。",
                    "入力エラー",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
                return;
            }

            var selectedPosition =
                (OverlayPositionComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString()
                ?? "BottomRight";

            var selectedMonitorIndex =
                DisplayMonitorComboBox.SelectedIndex >= 0
                ? DisplayMonitorComboBox.SelectedIndex
                : 0;

            ResultSettings = new AppSettings
            {
                Symbols = symbols,
                RefreshSeconds = refreshSeconds,
                HotkeyCtrl = CtrlCheckBox.IsChecked == true,
                HotkeyShift = ShiftCheckBox.IsChecked == true,
                HotkeyAlt = AltCheckBox.IsChecked == true,
                HotkeyWin = WinCheckBox.IsChecked == true,
                HotkeyKey = hotkeyKey,
                ShowOnlyWhileHotkeyHeld = ShowOnlyWhileHeldCheckBox.IsChecked == true,
                OverlayPosition = selectedPosition,
                OverlayMarginX = (int)Math.Round(marginX),
                OverlayMarginY = (int)Math.Round(marginY),
                DisplayMonitorIndex = selectedMonitorIndex
            };

            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}