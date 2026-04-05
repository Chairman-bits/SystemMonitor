using StealthStockOverlay.Models;
using StealthStockOverlay.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Input = System.Windows.Input;
using Media = System.Windows.Media;
using WinForms = System.Windows.Forms;

namespace StealthStockOverlay;

public partial class MainWindow : System.Windows.Window
{
    private const int HOTKEY_ID = 9000;
    private const int WM_HOTKEY = 0x0312;
    private const uint MOD_ALT = 0x0001;
    private const uint MOD_CONTROL = 0x0002;
    private const uint MOD_SHIFT = 0x0004;
    private const uint MOD_WIN = 0x0008;
    private const uint MOD_NOREPEAT = 0x4000;

    private readonly YahooFinanceService _yahooFinanceService = new();
    private readonly ObservableCollection<QuoteRow> _rows = new();
    private readonly DispatcherTimer _refreshTimer = new();
    private readonly DispatcherTimer _heldCheckTimer = new();
    private readonly WinForms.NotifyIcon _notifyIcon;
    private readonly HwndSourceHook _wndProcDelegate;

    private HwndSource? _source;
    private AppSettings _settings = new();
    private bool _isRefreshing;

    public MainWindow()
    {
        InitializeComponent();

        QuotesItemsControl.ItemsSource = _rows;
        _settings = SettingsService.Load();

        _refreshTimer.Tick += async (_, _) => await RefreshQuotesAsync();
        _heldCheckTimer.Interval = TimeSpan.FromMilliseconds(50);
        _heldCheckTimer.Tick += (_, _) =>
        {
            if (_settings.ShowOnlyWhileHotkeyHeld && IsVisible && !IsConfiguredHotkeyCurrentlyDown())
            {
                Hide();
            }
        };

        _notifyIcon = CreateNotifyIcon();
        _wndProcDelegate = WndProc;

        Loaded += async (_, _) =>
        {
            await AutoUpdater.CheckAsync();
            ApplyTimerSettings();
            UpdateHotkeyHelpText();
            await RefreshQuotesAsync();
            PositionOverlay();
        };

        Closing += MainWindow_Closing;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        _source = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
        _source?.AddHook(_wndProcDelegate);
        RegisterConfiguredHotkey(showError: true);
    }

    private void RegisterConfiguredHotkey(bool showError)
    {
        try
        {
            var handle = new WindowInteropHelper(this).Handle;
            UnregisterHotKey(handle, HOTKEY_ID);

            var modifiers = GetModifiers();
            var virtualKey = GetVirtualKey(_settings.HotkeyKey);

            if (modifiers == 0 || virtualKey == 0)
            {
                if (showError)
                {
                    System.Windows.MessageBox.Show(
                        "ホットキー設定が不正です。",
                        "ホットキー登録失敗",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Warning);
                }
                return;
            }

            var registered = RegisterHotKey(handle, HOTKEY_ID, modifiers | MOD_NOREPEAT, virtualKey);
            if (!registered && showError)
            {
                System.Windows.MessageBox.Show(
                    "ホットキー登録に失敗しました。\n他のアプリが使用している可能性があります。",
                    "ホットキー登録失敗",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
            }
        }
        catch
        {
            if (showError)
            {
                System.Windows.MessageBox.Show(
                    "ホットキー登録に失敗しました。",
                    "ホットキー登録失敗",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
            }
        }
    }

    private uint GetModifiers()
    {
        uint modifiers = 0;
        if (_settings.HotkeyCtrl) modifiers |= MOD_CONTROL;
        if (_settings.HotkeyShift) modifiers |= MOD_SHIFT;
        if (_settings.HotkeyAlt) modifiers |= MOD_ALT;
        if (_settings.HotkeyWin) modifiers |= MOD_WIN;
        return modifiers;
    }

    private static uint GetVirtualKey(string keyText)
    {
        var key = (keyText ?? "").Trim().ToUpperInvariant();
        if (key.Length != 1)
        {
            return 0;
        }

        var ch = key[0];
        if (ch >= 'A' && ch <= 'Z') return ch;
        if (ch >= '0' && ch <= '9') return ch;
        return 0;
    }

    private bool IsConfiguredHotkeyCurrentlyDown()
    {
        bool modifiersOk = true;

        if (_settings.HotkeyCtrl) modifiersOk &= IsKeyDown(0x11);
        if (_settings.HotkeyShift) modifiersOk &= IsKeyDown(0x10);
        if (_settings.HotkeyAlt) modifiersOk &= IsKeyDown(0x12);
        if (_settings.HotkeyWin) modifiersOk &= (IsKeyDown(0x5B) || IsKeyDown(0x5C));

        var vk = GetVirtualKey(_settings.HotkeyKey);
        if (vk == 0)
        {
            return false;
        }

        return modifiersOk && IsKeyDown((int)vk);
    }

    private static bool IsKeyDown(int vk)
    {
        return (GetAsyncKeyState(vk) & 0x8000) != 0;
    }

    private void UpdateHotkeyHelpText()
    {
        HotkeyHelpTextBlock.Text = $"{BuildHotkeyText()} で表示";
    }

    private string BuildHotkeyText()
    {
        var parts = new List<string>();
        if (_settings.HotkeyCtrl) parts.Add("Ctrl");
        if (_settings.HotkeyShift) parts.Add("Shift");
        if (_settings.HotkeyAlt) parts.Add("Alt");
        if (_settings.HotkeyWin) parts.Add("Win");
        parts.Add((_settings.HotkeyKey ?? "Z").ToUpperInvariant());
        return string.Join(" + ", parts);
    }

    private WinForms.NotifyIcon CreateNotifyIcon()
    {
        var menu = new WinForms.ContextMenuStrip();

        var refreshItem = new WinForms.ToolStripMenuItem("今すぐ更新", null, async (_, _) => await RefreshQuotesAsync());
        var updateStatusItem = new WinForms.ToolStripMenuItem("更新確認中...");
        var applyUpdateItem = new WinForms.ToolStripMenuItem("更新を適用", null, async (_, _) => await AutoUpdater.ApplyUpdateAsync());
        var releaseNotesItem = new WinForms.ToolStripMenuItem("更新履歴", null, async (_, _) => await AutoUpdater.ShowReleaseNotesAsync());
        var settingsItem = new WinForms.ToolStripMenuItem("設定", null, (_, _) => OpenSettings());
        var exitItem = new WinForms.ToolStripMenuItem("終了", null, (_, _) => ExitApplication());

        updateStatusItem.Enabled = false;
        applyUpdateItem.Visible = false;

        menu.Items.Add(refreshItem);
        menu.Items.Add(new WinForms.ToolStripSeparator());
        menu.Items.Add(updateStatusItem);
        menu.Items.Add(applyUpdateItem);
        menu.Items.Add(releaseNotesItem);
        menu.Items.Add(new WinForms.ToolStripSeparator());
        menu.Items.Add(settingsItem);
        menu.Items.Add(exitItem);

        System.Drawing.Icon trayIcon;
        try
        {
            var exePath = Environment.ProcessPath;
            trayIcon = !string.IsNullOrWhiteSpace(exePath) && System.IO.File.Exists(exePath)
                ? System.Drawing.Icon.ExtractAssociatedIcon(exePath) ?? System.Drawing.SystemIcons.Application
                : System.Drawing.SystemIcons.Application;
        }
        catch
        {
            trayIcon = System.Drawing.SystemIcons.Application;
        }

        var notifyIcon = new WinForms.NotifyIcon
        {
            Icon = trayIcon,
            Visible = true,
            Text = "System Monitor Helper",
            ContextMenuStrip = menu
        };

        notifyIcon.DoubleClick += (_, _) => ShowOverlay();

        _ = Task.Run(async () =>
        {
            await AutoUpdater.CheckSilentlyAsync();

            Dispatcher.Invoke(() =>
            {
                if (AutoUpdater.HasUpdate)
                {
                    updateStatusItem.Text = $"更新あり: {AutoUpdater.LatestVersionText}";
                    applyUpdateItem.Visible = true;
                }
                else
                {
                    updateStatusItem.Text = "更新なし";
                    applyUpdateItem.Visible = false;
                }
            });
        });

        return notifyIcon;
    }

    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        e.Cancel = true;
        Hide();
    }

    private void ExitApplication()
    {
        try
        {
            var handle = new WindowInteropHelper(this).Handle;
            UnregisterHotKey(handle, HOTKEY_ID);
        }
        catch
        {
        }

        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        System.Windows.Application.Current.Shutdown();
    }

    private void ApplyTimerSettings()
    {
        _refreshTimer.Stop();
        _refreshTimer.Interval = TimeSpan.FromSeconds(_settings.RefreshSeconds);
        _refreshTimer.Start();
    }

    private Rect GetSelectedMonitorWorkArea()
    {
        var screens = WinForms.Screen.AllScreens;
        if (screens.Length == 0)
        {
            return SystemParameters.WorkArea;
        }

        var index = Math.Clamp(_settings.DisplayMonitorIndex, 0, screens.Length - 1);
        var screen = screens[index];
        var wa = screen.WorkingArea;

        if (_source?.CompositionTarget is null)
        {
            return new Rect(wa.Left, wa.Top, wa.Width, wa.Height);
        }

        var transform = _source.CompositionTarget.TransformFromDevice;
        var topLeft = transform.Transform(new System.Windows.Point(wa.Left, wa.Top));
        var bottomRight = transform.Transform(new System.Windows.Point(wa.Right, wa.Bottom));

        return new Rect(topLeft, bottomRight);
    }

    private void PositionOverlay()
    {
        UpdateLayout();

        var workArea = GetSelectedMonitorWorkArea();

        var windowWidth = ActualWidth > 0 ? ActualWidth : Width;
        var windowHeight = ActualHeight > 0 ? ActualHeight : Height;

        var marginX = _settings.OverlayMarginX;
        var marginY = _settings.OverlayMarginY;

        double left;
        double top;

        switch (_settings.OverlayPosition)
        {
            case "BottomLeft":
                left = workArea.Left + marginX;
                top = workArea.Bottom - windowHeight - marginY;
                break;

            case "TopRight":
                left = workArea.Right - windowWidth - marginX;
                top = workArea.Top + marginY;
                break;

            case "TopLeft":
                left = workArea.Left + marginX;
                top = workArea.Top + marginY;
                break;

            case "MiddleRight":
                left = workArea.Right - windowWidth - marginX;
                top = workArea.Top + (workArea.Height - windowHeight) / 2;
                break;

            case "MiddleLeft":
                left = workArea.Left + marginX;
                top = workArea.Top + (workArea.Height - windowHeight) / 2;
                break;

            case "BottomRight":
            default:
                left = workArea.Right - windowWidth - marginX;
                top = workArea.Bottom - windowHeight - marginY;
                break;
        }

        if (left < workArea.Left) left = workArea.Left;
        if (top < workArea.Top) top = workArea.Top;
        if (left + windowWidth > workArea.Right) left = workArea.Right - windowWidth;
        if (top + windowHeight > workArea.Bottom) top = workArea.Bottom - windowHeight;

        Left = left;
        Top = top;
    }

    private void ShowOverlay()
    {
        Show();
        UpdateLayout();
        PositionOverlay();
        Activate();

        if (_settings.ShowOnlyWhileHotkeyHeld)
        {
            _heldCheckTimer.Stop();
            _heldCheckTimer.Start();
        }
    }

    private async Task RefreshQuotesAsync()
    {
        if (_isRefreshing)
        {
            return;
        }

        _isRefreshing = true;

        try
        {
            LastUpdatedTextBlock.Text = "更新中...";

            var quotes = await _yahooFinanceService.GetQuotesAsync(_settings.Symbols);

            _rows.Clear();
            foreach (var quote in quotes)
            {
                _rows.Add(new QuoteRow
                {
                    DisplaySymbol = quote.DisplaySymbol,
                    CompanyName = quote.CompanyName,
                    PriceText = quote.Price.ToString("0.##"),
                    ChangeText = $"{quote.Change:+0.##;-0.##;0} ({quote.ChangePercent:+0.##;-0.##;0}%)",
                    ChangeBrush = quote.Change > 0
                        ? Media.Brushes.LimeGreen
                        : quote.Change < 0
                            ? Media.Brushes.OrangeRed
                            : Media.Brushes.White
                });
            }

            if (_rows.Count == 0)
            {
                _rows.Add(new QuoteRow
                {
                    DisplaySymbol = "-",
                    CompanyName = "取得できません",
                    PriceText = "-",
                    ChangeText = "取得失敗",
                    ChangeBrush = Media.Brushes.OrangeRed
                });
            }

            LastUpdatedTextBlock.Text = $"最終更新: {DateTime.Now:HH:mm:ss}";
        }
        catch (Exception ex)
        {
            _rows.Clear();
            _rows.Add(new QuoteRow
            {
                DisplaySymbol = "-",
                CompanyName = "通信状態を確認",
                PriceText = "-",
                ChangeText = "通信エラー",
                ChangeBrush = Media.Brushes.OrangeRed
            });

            LastUpdatedTextBlock.Text = $"エラー: {TrimMessage(ex.Message)}";
        }
        finally
        {
            _isRefreshing = false;
        }
    }

    private static string TrimMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return "不明";
        }

        return message.Length <= 80 ? message : message[..80] + "...";
    }

    private void OpenSettings()
    {
        var window = new SettingsWindow(_settings);
        var result = window.ShowDialog();

        if (result == true && window.ResultSettings is not null)
        {
            _settings = window.ResultSettings;
            SettingsService.Save(_settings);
            ApplyTimerSettings();
            UpdateHotkeyHelpText();
            RegisterConfiguredHotkey(showError: true);
            PositionOverlay();
            _ = RefreshQuotesAsync();
        }
    }

    private void Window_KeyDown(object sender, Input.KeyEventArgs e)
    {
        if (e.Key == Input.Key.Escape)
        {
            Hide();
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
        {
            ShowOverlay();
            handled = true;
        }

        return IntPtr.Zero;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    public class QuoteRow
    {
        public string DisplaySymbol { get; set; } = "";
        public string CompanyName { get; set; } = "";
        public string PriceText { get; set; } = "";
        public string ChangeText { get; set; } = "";
        public Media.Brush ChangeBrush { get; set; } = Media.Brushes.White;
    }
}