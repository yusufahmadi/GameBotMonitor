using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using GameBotMonitor.Models;
using GameBotMonitor.Services;
using GameBotMonitor.Views;

namespace GameBotMonitor;

public partial class MainWindow : Window
{
    private AppConfig _config;
    private readonly DispatcherTimer _scanTimer;
    private readonly DispatcherTimer _hudRefreshTimer;  // Timer HUD refresh independen (tidak terpengaruh scan timer)
    private readonly SoundAlertService _soundService = new();
    private StorageCleanupService _storageService;
    private readonly TelegramBotListener _telegramListener = new();
    private readonly List<ClientSlot> _slots = new();
    private bool _isMonitoring = false;

    // System Tray
    private System.Windows.Forms.NotifyIcon _notifyIcon = null!;
    private System.Windows.Forms.ToolStripMenuItem _trayItemOpen = null!;
    private System.Windows.Forms.ToolStripMenuItem _trayItemExit = null!;
    private bool _closeFromTray = false;

    // Card Slot Text Brushes (Menyala saat Show Character Background ON)
    private static readonly System.Windows.Media.SolidColorBrush BrushPidDefault = CreateFrozenBrush(0x6C, 0x70, 0x86);
    private static readonly System.Windows.Media.SolidColorBrush BrushPidBright = CreateFrozenBrush(0x89, 0xDC, 0xEB);     // Menyala: Vivid Cyan (#89DCEB)
    private static readonly System.Windows.Media.SolidColorBrush BrushTitleDefault = CreateFrozenBrush(0xBA, 0xC2, 0xDE);
    private static readonly System.Windows.Media.SolidColorBrush BrushTitleBright = CreateFrozenBrush(0xFF, 0xFF, 0xFF);   // Menyala: Pure White (#FFFFFF)
    private static readonly System.Windows.Media.SolidColorBrush BrushHpSampleDefault = CreateFrozenBrush(0x6C, 0x70, 0x86);
    private static readonly System.Windows.Media.SolidColorBrush BrushHpSampleBright = CreateFrozenBrush(0xF9, 0xE2, 0xAF); // Menyala: Warm Gold / Yellow (#F9E2AF)

    private static System.Windows.Media.SolidColorBrush CreateFrozenBrush(byte r, byte g, byte b)
    {
        var brush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }

    public MainWindow()
    {
        InitializeComponent();

        _config = ConfigService.Load();
        _storageService = new StorageCleanupService(_config.Storage.BaseFolder, _config.Storage.MaxDaysRetention, _config.Storage.MaxScreenshotsPerDay);

        // Inisialisasi 3 slot default (ON by default)
        for (int i = 1; i <= 3; i++)
        {
            _slots.Add(new ClientSlot { SlotIndex = i, IsEnabled = true });
        }

        // Timer pemindaian layar
        _scanTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(Math.Max(500, _config.ScanIntervalMs))
        };
        _scanTimer.Tick += ScanTimer_Tick;

        // Timer refresh HUD background kartu slot — independen dari scan, selalu jalan jika ShowHudBackground aktif
        _hudRefreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(3)
        };
        _hudRefreshTimer.Tick += HudRefreshTimer_Tick;

        // Inisialisasi bahasa dari config
        LanguageService.CurrentLanguage = _config.Language ?? "id";

        // System Tray
        InitNotifyIcon();
        this.StateChanged += MainWindow_StateChanged;

        LoadConfigToUi();
        ApplyLanguage();
        UpdateSlotUi();

        // Start HUD refresh timer jika ShowHudBackground aktif dari config
        if (_config.ShowHudBackground)
        {
            _hudRefreshTimer.Start();
        }

        // Inisialisasi Telegram Remote Control (2-Way)
        InitTelegramListener();

        AddLog("Aplikasi siap / Application ready.");
    }

    // ===================== SYSTEM TRAY =====================

    private void InitNotifyIcon()
    {
        string trayText = $"{_config?.GameName ?? "GF"} Bot Monitor";
        if (trayText.Length >= 64) trayText = trayText.Substring(0, 63);

        System.Drawing.Icon? appIcon = null;
        try
        {
            string iconPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app_icon.ico");
            if (System.IO.File.Exists(iconPath))
            {
                appIcon = new System.Drawing.Icon(iconPath);
            }
            else if (!string.IsNullOrEmpty(Environment.ProcessPath))
            {
                appIcon = System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath);
            }
        }
        catch
        {
            appIcon = null;
        }

        _notifyIcon = new System.Windows.Forms.NotifyIcon
        {
            Text = trayText,
            Visible = true,
            Icon = appIcon ?? System.Drawing.SystemIcons.Information
        };

        var contextMenu = new System.Windows.Forms.ContextMenuStrip();
        _trayItemOpen = new System.Windows.Forms.ToolStripMenuItem(LanguageService.Get("TrayOpenApp"), null, (_, _) => ShowMainWindow());
        _trayItemExit = new System.Windows.Forms.ToolStripMenuItem(LanguageService.Get("TrayExit"), null, (_, _) =>
        {
            _closeFromTray = true;
            this.Close();
        });

        contextMenu.Items.Add(_trayItemOpen);
        contextMenu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        contextMenu.Items.Add(_trayItemExit);

        _notifyIcon.ContextMenuStrip = contextMenu;
        _notifyIcon.DoubleClick += (_, _) => ShowMainWindow();
    }

    private void ShowMainWindow()
    {
        this.Show();
        this.WindowState = WindowState.Normal;
        this.Activate();
    }

    private void MainWindow_StateChanged(object? sender, EventArgs e)
    {
        if (this.WindowState == WindowState.Minimized)
        {
            this.Hide();
            _notifyIcon.ShowBalloonTip(
                2000,
                LanguageService.Get("TrayBalloonTitle"),
                LanguageService.Get("TrayBalloonText"),
                System.Windows.Forms.ToolTipIcon.Info
            );
        }
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (!_closeFromTray)
        {
            // Minimize to tray instead of close when user clicks X
            e.Cancel = true;
            this.WindowState = WindowState.Minimized;
        }
        else
        {
            base.OnClosing(e);
        }
    }

    // ===================== LANGUAGE SWITCHING =====================

    private void CmbLanguage_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (CmbLanguage?.SelectedItem is System.Windows.Controls.ComboBoxItem item)
        {
            string lang = item.Tag?.ToString() ?? "id";
            if (_config != null && _config.Language != lang)
            {
                _config.Language = lang;
                LanguageService.CurrentLanguage = lang;
                ApplyLanguage();
                ConfigService.Save(_config);
                AddLog($"[INFO] Bahasa diubah ke / Language changed to: {(lang == "en" ? "English" : "Bahasa Indonesia")}");
            }
        }
    }

    private void ApplyLanguage()
    {
        // Header
        string appTitle = LanguageService.Format("AppTitle", _config?.GameName ?? "Game");
        this.Title = appTitle;
        TxtAppTitle.Text = appTitle;
        TxtAppSubtitle.Text = LanguageService.Get("AppSubtitle");

        // Toolbar Buttons
        BtnToggleMonitoring.Content = _isMonitoring ? LanguageService.Get("BtnStopMonitor") : LanguageService.Get("BtnStartMonitor");
        BtnAutoTile.Content = LanguageService.Get("BtnAutoTile");
        BtnCalibrate.Content = LanguageService.Get("BtnCalibrate");
        BtnStopAlarm.Content = LanguageService.Get("BtnStopAlarm");

        // Slot Cards
        TxtSlot1Title.Text = LanguageService.Get("Slot1Title");
        TxtSlot2Title.Text = LanguageService.Get("Slot2Title");
        TxtSlot3Title.Text = LanguageService.Get("Slot3Title");

        // Refresh slot status & badges
        foreach (var slot in _slots)
        {
            slot.RefreshLanguage();
            if (slot.Status == ClientStatus.Offline)
            {
                slot.WindowTitle = LanguageService.Get("WaitingClient");
                slot.LastInfo = LanguageService.Get("ClientNotDetected");
            }
        }
        UpdateSlotUi();

        // Tabs
        TabLogsItem.Header = LanguageService.Get("TabLogs");
        TabSettingsItem.Header = LanguageService.Get("TabSettings");
        BtnClearLogs.Content = LanguageService.Get("BtnClearLogs");

        // Settings - Language
        SecLanguageHeader.Text = LanguageService.Get("SecLanguage");
        LblLanguage.Text = LanguageService.Get("LblLanguage");

        // Settings - Game & Window Target
        SecGameTargetHeader.Text = LanguageService.Get("SecGameTarget");
        LblGameName.Text = LanguageService.Get("LblGameName");
        HintGameName.Text = LanguageService.Get("HintGameName");
        LblTargetTitle.Text = LanguageService.Get("LblTargetTitle");
        HintTargetTitle.Text = LanguageService.Get("HintTargetTitle");
        ChkShowHudBackground.Content = LanguageService.Get("ChkShowHudBackground");
        HintShowHudBackground.Text = LanguageService.Get("HintShowHudBackground");

        // Settings - Detection
        SecDetectionHeader.Text = LanguageService.Get("SecDetection");
        LblDelaySeconds.Text = LanguageService.Get("LblDelaySeconds");
        HintDelaySeconds.Text = LanguageService.Get("HintDelaySeconds");
        LblScanInterval.Text = LanguageService.Get("LblScanInterval");
        HintScanInterval.Text = LanguageService.Get("HintScanInterval");
        LblCooldownMinutes.Text = LanguageService.Get("LblCooldownMinutes");
        HintCooldownMinutes.Text = LanguageService.Get("HintCooldownMinutes");
        LblHpCoords.Text = LanguageService.Get("LblHpCoords");
        HintHpCoords.Text = LanguageService.Get("HintHpCoords");

        // Settings - Auto TAB
        SecAutoTabHeader.Text = "🎯 " + LanguageService.Get("SecAutoTab");
        ChkAutoTabEnabled.Content = LanguageService.Get("ChkAutoTab");
        HintAutoTab.Text = LanguageService.Get("HintAutoTab");
        LblAutoTabMode.Text = LanguageService.Get("LblAutoTabMode");
        HintAutoTabMode.Text = LanguageService.Get("HintAutoTabMode");
        LblAutoTabIdle.Text = LanguageService.Get("LblAutoTabIdle");
        HintAutoTabIdle.Text = LanguageService.Get("HintAutoTabIdle");
        LblAutoTabInterval.Text = LanguageService.Get("LblAutoTabInterval");
        HintAutoTabInterval.Text = LanguageService.Get("HintAutoTabInterval");
        LblAutoTabCoords.Text = LanguageService.Get("LblAutoTabCoords");
        HintAutoTabCoords.Text = LanguageService.Get("HintAutoTabCoords");
        BtnCalibrateTarget.Content = "🎯 " + LanguageService.Get("BtnCalibrateTarget");

        // Settings - Diagnostics / Debug
        SecDiagnosticsHeader.Text = LanguageService.Get("SecDiagnostics");
        ChkEnableDebugLog.Content = LanguageService.Get("ChkEnableDebugLog");
        HintEnableDebugLog.Text = LanguageService.Get("HintEnableDebugLog");
        LblAutoTabSlot1.Text = "🎯 " + LanguageService.Get("AutoTabToggleLabel") + ":";
        LblAutoTabSlot2.Text = "🎯 " + LanguageService.Get("AutoTabToggleLabel") + ":";
        LblAutoTabSlot3.Text = "🎯 " + LanguageService.Get("AutoTabToggleLabel") + ":";
        BtnTestTabSlot1.Content = "⚡ " + LanguageService.Get("BtnTestTab");
        BtnTestTabSlot2.Content = "⚡ " + LanguageService.Get("BtnTestTab");
        BtnTestTabSlot3.Content = "⚡ " + LanguageService.Get("BtnTestTab");

        // Settings - Discord
        SecDiscordHeader.Text = LanguageService.Get("SecDiscord");
        ChkDiscordEnabled.Content = LanguageService.Get("ChkDiscord");
        ChkDiscordMention.Content = LanguageService.Get("ChkMention");
        BtnTestDiscord.Content = LanguageService.Get("BtnTestDiscord");

        // Settings - Telegram
        SecTelegramHeader.Text = LanguageService.Get("SecTelegram");
        ChkTelegramEnabled.Content = LanguageService.Get("ChkTelegram");
        ChkTelegramInteractive.Content = LanguageService.Get("ChkTelegramInteractive");
        HintTelegramInteractive.Text = LanguageService.Get("HintTelegramInteractive");
        LblTelegramToken.Text = LanguageService.Get("LblTelegramToken");
        LblTelegramChatId.Text = LanguageService.Get("LblTelegramChatId");
        BtnTestTelegram.Content = LanguageService.Get("BtnTestTelegram");

        // Settings - Sound
        SecSoundHeader.Text = LanguageService.Get("SecSound");
        ChkSoundEnabled.Content = LanguageService.Get("ChkSound");
        BtnBrowseSound.Content = LanguageService.Get("BtnBrowseSound");
        BtnTestSound.Content = LanguageService.Get("BtnTestSound");
        HintSound.Text = LanguageService.Get("HintSound");

        // Settings - Storage
        SecStorageHeader.Text = LanguageService.Get("SecStorage");
        ChkCropHudOnly.Content = LanguageService.Get("ChkCropHud");
        TxtStorageInfo1.Text = LanguageService.Get("StorageInfo1");
        TxtStorageInfo2.Text = LanguageService.Get("StorageInfo2");
        TxtStorageInfo3.Text = LanguageService.Get("StorageInfo3");
        BtnSaveSettings.Content = LanguageService.Get("BtnSaveSettings");

        // Tray Menu & Tooltip
        string trayText = $"{_config?.GameName ?? "GF"} Bot Monitor";
        if (trayText.Length >= 64) trayText = trayText.Substring(0, 63);
        if (_notifyIcon != null) _notifyIcon.Text = trayText;

        if (_trayItemOpen != null) _trayItemOpen.Text = LanguageService.Get("TrayOpenApp");
        if (_trayItemExit != null) _trayItemExit.Text = LanguageService.Get("TrayExit");
    }

    // ===================== SLOT ON/OFF TOGGLE =====================

    public bool SetSlotMonitoring(int slotNum, bool enable)
    {
        if (slotNum < 1 || slotNum > 3) return false;
        var slot = _slots[slotNum - 1];
        slot.IsEnabled = enable;

        WpfButton? btn = slotNum switch
        {
            1 => BtnToggleSlot1,
            2 => BtnToggleSlot2,
            3 => BtnToggleSlot3,
            _ => null
        };

        if (btn != null)
        {
            if (enable)
            {
                btn.Content = "\u25CF ON";
                btn.Background = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#2E7D32"));
                slot.LastAlertSentTime = null;
                slot.DeadSince = null;
            }
            else
            {
                btn.Content = "\u25CB OFF";
                btn.Background = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#6C7086"));
            }
        }

        string logMsg = enable ? LanguageService.Get("SlotEnabledLog") : LanguageService.Get("SlotDisabledLog");
        AddLog($"[INFO] Slot {slotNum} {logMsg}");
        return true;
    }

    private void BtnToggleSlot_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not WpfButton btn) return;
        if (!int.TryParse(btn.Tag?.ToString(), out int slotNum)) return;
        var slot = _slots[slotNum - 1];
        SetSlotMonitoring(slotNum, !slot.IsEnabled);
    }

    // ===================== AUTO TAB TOGGLE PER SLOT =====================

    public bool SetAutoTabSlot(int slotNum, bool enable)
    {
        if (slotNum < 1 || slotNum > 3) return false;
        var slot = _slots[slotNum - 1];
        slot.IsAutoTabEnabled = enable;
        slot.NoTargetSince = null;
        slot.LastTabSentTime = null;

        WpfButton? btn = slotNum switch
        {
            1 => BtnAutoTabSlot1,
            2 => BtnAutoTabSlot2,
            3 => BtnAutoTabSlot3,
            _ => null
        };

        if (btn != null)
        {
            if (enable)
            {
                btn.Content = "● ON";
                btn.Background = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#1E88E5"));
                btn.Foreground = System.Windows.Media.Brushes.White;
            }
            else
            {
                btn.Content = "○ OFF";
                btn.Background = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#45475A"));
                btn.Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#CDD6F4"));
            }
        }

        string statusStr = enable ? "diaktifkan (ON)" : "dinonaktifkan (OFF)";
        AddLog($"[INFO] Slot {slotNum}: Auto TAB {statusStr}.");
        return true;
    }

    private void BtnToggleAutoTabSlot_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not WpfButton btn) return;
        if (!int.TryParse(btn.Tag?.ToString(), out int slotNum)) return;
        var slot = _slots[slotNum - 1];
        SetAutoTabSlot(slotNum, !slot.IsAutoTabEnabled);
    }

    public static void SendHardwareKey(nint hWnd, ushort scanCode, int holdMs = 150)
    {
        try
        {
            if (hWnd == 0) return;
            Native.Win32.SetForegroundWindow(hWnd);
            System.Threading.Thread.Sleep(80);

            int structSize = System.Runtime.InteropServices.Marshal.SizeOf(typeof(Native.Win32.INPUT));

            var inputDown = new Native.Win32.INPUT[1];
            inputDown[0] = new Native.Win32.INPUT
            {
                type = Native.Win32.INPUT_KEYBOARD,
                u = new Native.Win32.InputUnion
                {
                    ki = new Native.Win32.KEYBDINPUT
                    {
                        wVk = 0,
                        wScan = scanCode,
                        dwFlags = Native.Win32.KEYEVENTF_SCANCODE,
                        time = 0,
                        dwExtraInfo = IntPtr.Zero
                    }
                }
            };
            Native.Win32.SendInput(1, inputDown, structSize);

            System.Threading.Thread.Sleep(holdMs);

            var inputUp = new Native.Win32.INPUT[1];
            inputUp[0] = new Native.Win32.INPUT
            {
                type = Native.Win32.INPUT_KEYBOARD,
                u = new Native.Win32.InputUnion
                {
                    ki = new Native.Win32.KEYBDINPUT
                    {
                        wVk = 0,
                        wScan = scanCode,
                        dwFlags = Native.Win32.KEYEVENTF_SCANCODE | Native.Win32.KEYEVENTF_KEYUP,
                        time = 0,
                        dwExtraInfo = IntPtr.Zero
                    }
                }
            };
            Native.Win32.SendInput(1, inputUp, structSize);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SendHardwareKey] Error: {ex.Message}");
        }
    }

    public static void SendTabKey(nint hWnd, string mode)
    {
        try
        {
            if (hWnd == 0) return;

            if (mode == "Foreground")
            {
                SendHardwareKey(hWnd, 0x0F, 150);
            }
            else
            {
                // Mode A (Background): Kirim WM_CHAR + WM_KEYDOWN/WM_KEYUP ke message queue jendela game
                nint wParam = (nint)Native.Win32.VK_TAB;
                nint lParamDown = (nint)0x000F0001; // Scan code 0x0F, repeat 1
                nint lParamUp = unchecked((nint)0xC00F0001); // Bit 30+31 = KeyUp

                Native.Win32.PostMessage(hWnd, Native.Win32.WM_KEYDOWN, wParam, lParamDown);
                System.Threading.Thread.Sleep(20);
                Native.Win32.PostMessage(hWnd, Native.Win32.WM_KEYUP, wParam, lParamUp);
                System.Threading.Thread.Sleep(15);
                Native.Win32.PostMessage(hWnd, Native.Win32.WM_CHAR, wParam, lParamDown);
                //// Kirim WM_CHAR sebagai karakter hasil dari KeyDown
                //Native.Win32.PostMessage(hWnd, Native.Win32.WM_CHAR, wParam, lParamDown);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SendTabKey] Error: {ex.Message}");
        }
    }

    private void LoadConfigToUi()
    {
        // Language
        CmbLanguage.SelectedIndex = (_config.Language?.ToLowerInvariant() == "en") ? 1 : 0;

        TxtScanInterval.Text = _config.ScanIntervalMs.ToString();
        TxtDelaySeconds.Text = _config.ConfirmationDelaySeconds.ToString("0.0");
        TxtCooldownMinutes.Text = _config.CooldownMinutes.ToString();
        TxtHpX.Text = _config.HpOffsetX.ToString();
        TxtHpY.Text = _config.HpOffsetY.ToString();
        TxtGameName.Text = _config.GameName;
        TxtTargetTitle.Text = _config.TargetWindowTitle;
        ChkShowHudBackground.IsChecked = _config.ShowHudBackground;
        ChkEnableDebugLog.IsChecked = _config.EnableDebugLog;

        // Auto TAB
        ChkAutoTabEnabled.IsChecked = _config.AutoTab.Enabled;
        CmbAutoTabMode.SelectedIndex = (_config.AutoTab.SendMode == "Foreground") ? 1 : 0;
        TxtAutoTabIdle.Text = _config.AutoTab.IdleSeconds.ToString("0.0");
        TxtAutoTabInterval.Text = _config.AutoTab.IntervalSeconds.ToString("0.0");
        TxtAutoTabTargetX.Text = _config.AutoTab.TargetOffsetX.ToString();
        TxtAutoTabTargetY.Text = _config.AutoTab.TargetOffsetY.ToString();

        ChkDiscordEnabled.IsChecked = _config.Discord.Enabled;
        TxtDiscordWebhook.Text = _config.Discord.WebhookUrl;
        ChkDiscordMention.IsChecked = _config.Discord.MentionEveryone;

        ChkTelegramEnabled.IsChecked = _config.Telegram.Enabled;
        ChkTelegramInteractive.IsChecked = _config.Telegram.InteractiveEnabled;
        TxtTelegramToken.Text = _config.Telegram.BotToken;
        TxtTelegramChatId.Text = _config.Telegram.ChatId;

        ChkSoundEnabled.IsChecked = _config.SoundAlert.Enabled;
        TxtAudioPath.Text = _config.SoundAlert.CustomAudioPath;

        ChkCropHudOnly.IsChecked = _config.Storage.CropHudOnly;

        // Character Switch Mapping
        TxtLogoutOffsetY.Text = _config.CharSwitch.LogoutOffsetY.ToString();
        TxtReturnOffsetX.Text = _config.CharSwitch.ReturnOffsetX.ToString();
        TxtReturnOffsetY.Text = _config.CharSwitch.ReturnOffsetY.ToString();
        TxtStartGamePercentX.Text = _config.CharSwitch.StartGamePercentX.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
        TxtStartGamePercentY.Text = _config.CharSwitch.StartGamePercentY.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);

        TxtCard1PercentX.Text = _config.CharSwitch.Card1PercentX.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
        TxtCard1PercentY.Text = _config.CharSwitch.Card1PercentY.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
        TxtCard2PercentX.Text = _config.CharSwitch.Card2PercentX.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
        TxtCard2PercentY.Text = _config.CharSwitch.Card2PercentY.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
        TxtCard3PercentX.Text = _config.CharSwitch.Card3PercentX.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
        TxtCard3PercentY.Text = _config.CharSwitch.Card3PercentY.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);

        TxtPagePrevPercentX.Text = _config.CharSwitch.PagePrevPercentX.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
        TxtPagePrevPercentY.Text = _config.CharSwitch.PagePrevPercentY.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
        TxtPageNextPercentX.Text = _config.CharSwitch.PageNextPercentX.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
        TxtPageNextPercentY.Text = _config.CharSwitch.PageNextPercentY.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);

        // Clear SML (Sprite Magic Land)
        TxtSmlHotkey.Text = _config.Sml.DungeonHotkey ?? ",";
        TxtTabSmlPercentX.Text = _config.Sml.TabSmlPercentX.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
        TxtTabSmlPercentY.Text = _config.Sml.TabSmlPercentY.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);

        TxtSmlLv1PercentX.Text = _config.Sml.Level1PercentX.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
        TxtSmlLv1PercentY.Text = _config.Sml.Level1PercentY.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
        TxtSmlLv2PercentX.Text = _config.Sml.Level2PercentX.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
        TxtSmlLv2PercentY.Text = _config.Sml.Level2PercentY.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
        TxtSmlLv3PercentX.Text = _config.Sml.Level3PercentX.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
        TxtSmlLv3PercentY.Text = _config.Sml.Level3PercentY.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
        TxtSmlLv4PercentX.Text = _config.Sml.Level4PercentX.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
        TxtSmlLv4PercentY.Text = _config.Sml.Level4PercentY.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);

        TxtSmlTowerPercentX.Text = _config.Sml.TowerCenterPercentX.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
        TxtSmlTowerPercentY.Text = _config.Sml.TowerCenterPercentY.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);

        TxtSmlGatePercentX.Text = _config.Sml.GatePercentX.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
        TxtSmlGatePercentY.Text = _config.Sml.GatePercentY.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);

        TxtSmlEnterPercentX.Text = _config.Sml.EnterButtonPercentX.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
        TxtSmlEnterPercentY.Text = _config.Sml.EnterButtonPercentY.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
    }

    private void UpdateSlotUi()
    {
        // Slot 1
        UpdateCard(_slots[0], TxtPid1, TxtTitle1, BadgeBorder1, BadgeText1, ColorSwatch1, TxtHex1, TxtInfo1, ImgCardBg1, OverlayCardBg1);
        // Slot 2
        UpdateCard(_slots[1], TxtPid2, TxtTitle2, BadgeBorder2, BadgeText2, ColorSwatch2, TxtHex2, TxtInfo2, ImgCardBg2, OverlayCardBg2);
        // Slot 3
        UpdateCard(_slots[2], TxtPid3, TxtTitle3, BadgeBorder3, BadgeText3, ColorSwatch3, TxtHex3, TxtInfo3, ImgCardBg3, OverlayCardBg3);
    }

    private void UpdateCard(ClientSlot slot,
        System.Windows.Controls.TextBlock txtPid,
        System.Windows.Controls.TextBlock txtTitle,
        System.Windows.Controls.Border badgeBorder,
        System.Windows.Controls.TextBlock badgeText,
        System.Windows.Controls.Border colorSwatch,
        System.Windows.Controls.TextBlock txtHex,
        System.Windows.Controls.TextBlock txtInfo,
        System.Windows.Controls.Image imgCardBg,
        System.Windows.Controls.Border overlayCardBg)
    {
        txtPid.Text = slot.ProcessId > 0 ? $"PID: {slot.ProcessId}" : "PID: -";
        txtTitle.Text = slot.WindowTitle;
        badgeText.Text = slot.StatusBadgeText;

        try
        {
            badgeBorder.Background = (System.Windows.Media.Brush)new BrushConverter().ConvertFromString(slot.StatusBadgeBackground)!;
            colorSwatch.Background = (System.Windows.Media.Brush)new BrushConverter().ConvertFromString(slot.HealthColorHex)!;
        }
        catch
        {
            badgeBorder.Background = System.Windows.Media.Brushes.Gray;
            colorSwatch.Background = System.Windows.Media.Brushes.Black;
        }

        txtHex.Text = slot.HealthColorHex;
        txtInfo.Text = slot.LastInfo;

        // Tampilan Background HUD Karakter (Opsional — initial load; refresh rutin ditangani oleh _hudRefreshTimer)
        if (_config.ShowHudBackground && slot.Status != ClientStatus.Offline && slot.WindowHandle != 0)
        {
            try
            {
                using var bmp = PixelHealthScanner.CaptureWindow(slot.WindowHandle, true, _config.Storage.CropWidth, _config.Storage.CropHeight);
                if (bmp != null)
                {
                    imgCardBg.Source = BitmapToBitmapSource(bmp);
                    imgCardBg.Visibility = Visibility.Visible;
                    overlayCardBg.Visibility = Visibility.Visible;
                }
            }
            catch
            {
                imgCardBg.Visibility = Visibility.Collapsed;
                overlayCardBg.Visibility = Visibility.Collapsed;
            }
        }
        else
        {
            imgCardBg.Source = null;
            imgCardBg.Visibility = Visibility.Collapsed;
            overlayCardBg.Visibility = Visibility.Collapsed;
        }

        // Warna teks lebih menyala & tegas saat fitur Show Character Background aktif
        if (_config.ShowHudBackground)
        {
            txtPid.Foreground = BrushPidBright;
            txtPid.FontWeight = FontWeights.SemiBold;

            txtTitle.Foreground = BrushTitleBright;
            txtTitle.FontWeight = FontWeights.SemiBold;

            txtHex.Foreground = BrushHpSampleBright;
            txtHex.FontWeight = FontWeights.SemiBold;
        }
        else
        {
            txtPid.Foreground = BrushPidDefault;
            txtPid.FontWeight = FontWeights.Normal;

            txtTitle.Foreground = BrushTitleDefault;
            txtTitle.FontWeight = FontWeights.Normal;

            txtHex.Foreground = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(0xCD, 0xD6, 0xF4)); // #CDD6F4 default
            txtHex.FontWeight = FontWeights.SemiBold;
        }
    }

    /// <summary>
    /// Timer HUD Refresh — hanya memperbarui gambar background kartu slot (bukan full scan HP).
    /// Berjalan setiap 3 detik, independen dari scan timer, sehingga tetap update meski scan di-pause.
    /// </summary>
    private void HudRefreshTimer_Tick(object? sender, EventArgs e)
    {
        if (!_config.ShowHudBackground) return;

        RefreshHudBackground(ImgCardBg1, OverlayCardBg1, _slots[0]);
        RefreshHudBackground(ImgCardBg2, OverlayCardBg2, _slots[1]);
        RefreshHudBackground(ImgCardBg3, OverlayCardBg3, _slots[2]);
    }

    private void RefreshHudBackground(
        System.Windows.Controls.Image imgCardBg,
        System.Windows.Controls.Border overlayCardBg,
        ClientSlot slot)
    {
        if (slot.Status == ClientStatus.Offline || slot.WindowHandle == 0)
        {
            imgCardBg.Source = null;
            imgCardBg.Visibility = Visibility.Collapsed;
            overlayCardBg.Visibility = Visibility.Collapsed;
            return;
        }

        try
        {
            using var bmp = PixelHealthScanner.CaptureWindow(
                slot.WindowHandle, true,
                _config.Storage.CropWidth,
                _config.Storage.CropHeight);

            if (bmp != null)
            {
                imgCardBg.Source = BitmapToBitmapSource(bmp);
                imgCardBg.Visibility = Visibility.Visible;
                overlayCardBg.Visibility = Visibility.Visible;
            }
        }
        catch
        {
            imgCardBg.Visibility = Visibility.Collapsed;
            overlayCardBg.Visibility = Visibility.Collapsed;
        }
    }

    private static System.Windows.Media.Imaging.BitmapSource BitmapToBitmapSource(Bitmap bitmap)
    {
        var hBitmap = bitmap.GetHbitmap();
        try
        {
            return System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                hBitmap,
                IntPtr.Zero,
                Int32Rect.Empty,
                System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());
        }
        finally
        {
            Native.Win32.DeleteObject(hBitmap);
        }
    }

    private void AddLog(string message)
    {
        if (message.Contains("[DEBUG]") && !(_config?.EnableDebugLog ?? false))
        {
            return;
        }

        Dispatcher.Invoke(() =>
        {
            var line = $"[{DateTime.Now:HH:mm:ss}] {message}\n";
            TxtLogs.AppendText(line);
            TxtLogs.ScrollToEnd();
        });
    }

    private void BtnToggleMonitoring_Click(object sender, RoutedEventArgs e)
    {
        if (!_isMonitoring)
        {
            // Auto-save pengaturan dari antarmuka agar semua input aktif seketika
            SaveSettingsFromUi(silent: true);

            foreach (var slot in _slots)
            {
                slot.DeadSince = null;
                slot.LastAlertSentTime = null;
            }

            _scanTimer.Start();
            _isMonitoring = true;
            BtnToggleMonitoring.Content = LanguageService.Get("BtnStopMonitor");
            BtnToggleMonitoring.Style = (Style)FindResource("DangerButton");
            AddLog("[INFO] Pemantauan dimulai / Monitoring started.");
        }
        else
        {
            _scanTimer.Stop();
            _isMonitoring = false;
            BtnToggleMonitoring.Content = LanguageService.Get("BtnStartMonitor");
            BtnToggleMonitoring.Style = (Style)FindResource("PrimaryButton");
            _soundService.Stop();
            BtnStopAlarm.Visibility = Visibility.Collapsed;
            AddLog("[INFO] Pemantauan dihentikan / Monitoring stopped.");
        }
    }

    private void BtnAutoTile_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            int count = AutoTileService.TileGameWindows(_config.TargetWindowTitle);
            if (count > 0)
            {
                AddLog($"[INFO] Berhasil merapikan {count} jendela game / Tiled {count} game windows.");
            }
            else
            {
                AddLog($"[WARN] Tidak ditemukan jendela game dengan judul '{_config.TargetWindowTitle}'.");
            }
        }
        catch (Exception ex)
        {
            AddLog($"[ERROR] Gagal merapikan jendela: {ex.Message}");
        }
    }

    private void BtnCalibrate_Click(object sender, RoutedEventArgs e)
    {
        var overlay = new CalibrationOverlay(_config.TargetWindowTitle, _config.GameName);
        overlay.ShowDialog();

        if (overlay.IsSuccess)
        {
            _config.HpOffsetX = overlay.ResultOffsetX;
            _config.HpOffsetY = overlay.ResultOffsetY;
            TxtHpX.Text = _config.HpOffsetX.ToString();
            TxtHpY.Text = _config.HpOffsetY.ToString();

            ConfigService.Save(_config);
            AddLog($"[INFO] Kalibrasi disimpan: Offset X = {_config.HpOffsetX}, Offset Y = {_config.HpOffsetY}");
        }
    }

    private void BtnCalibrateTarget_Click(object sender, RoutedEventArgs e)
    {
        var overlay = new CalibrationOverlay(
            _config.TargetWindowTitle, 
            _config.GameName, 
            LanguageService.Get("MsgCalibrateTargetPrompt"));
        overlay.ShowDialog();

        if (overlay.IsSuccess)
        {
            _config.AutoTab.TargetOffsetX = overlay.ResultOffsetX;
            _config.AutoTab.TargetOffsetY = overlay.ResultOffsetY;
            TxtAutoTabTargetX.Text = _config.AutoTab.TargetOffsetX.ToString();
            TxtAutoTabTargetY.Text = _config.AutoTab.TargetOffsetY.ToString();

            ConfigService.Save(_config);
            AddLog($"[INFO] Kalibrasi Target Monster disimpan: Offset X = {_config.AutoTab.TargetOffsetX}, Offset Y = {_config.AutoTab.TargetOffsetY}");
        }
    }

    private void BtnCalibrateLogout_Click(object sender, RoutedEventArgs e)
    {
        var overlay = new CalibrationOverlay(
            _config.TargetWindowTitle,
            _config.GameName,
            "Buka menu System (tekan ESC) di game, lalu KLIK 1 KALI tepat di tengah tombol LOGOUT.",
            showDefaultSuccessDialog: false);
        overlay.ShowDialog();

        if (overlay.IsSuccess)
        {
            _config.CharSwitch.LogoutOffsetX = overlay.CenterOffsetX;
            _config.CharSwitch.LogoutOffsetY = overlay.CenterOffsetY;
            TxtLogoutOffsetY.Text = _config.CharSwitch.LogoutOffsetY.ToString();

            ConfigService.Save(_config);
            AddLog($"[INFO] Kalibrasi Logout disimpan: Offset Y = {_config.CharSwitch.LogoutOffsetY} px (Offset X = {_config.CharSwitch.LogoutOffsetX} px)");

            WpfMessageBox.Show(
                $"Kalibrasi Tombol Logout Berhasil!\n\n" +
                $"Offset Y dari Center: {_config.CharSwitch.LogoutOffsetY} px\n" +
                $"Offset X dari Center: {_config.CharSwitch.LogoutOffsetX} px\n\n" +
                $"Pengaturan telah disimpan otomatis.",
                "Sukses Kalibrasi Logout",
                WpfMessageBoxButton.OK,
                WpfMessageBoxImage.Information);
        }
    }

    private void BtnCalibrateReturn_Click(object sender, RoutedEventArgs e)
    {
        var overlay = new CalibrationOverlay(
            _config.TargetWindowTitle,
            _config.GameName,
            "Saat dialog Quit (countdown 10s) muncul di game, KLIK 1 KALI tepat di tengah tombol RETURN.",
            showDefaultSuccessDialog: false);
        overlay.ShowDialog();

        if (overlay.IsSuccess)
        {
            _config.CharSwitch.ReturnOffsetX = overlay.CenterOffsetX;
            _config.CharSwitch.ReturnOffsetY = overlay.CenterOffsetY;
            TxtReturnOffsetX.Text = _config.CharSwitch.ReturnOffsetX.ToString();
            TxtReturnOffsetY.Text = _config.CharSwitch.ReturnOffsetY.ToString();

            ConfigService.Save(_config);
            AddLog($"[INFO] Kalibrasi Return disimpan: Offset X = {_config.CharSwitch.ReturnOffsetX} px, Offset Y = {_config.CharSwitch.ReturnOffsetY} px");

            WpfMessageBox.Show(
                $"Kalibrasi Tombol Return Berhasil!\n\n" +
                $"Offset X dari Center: {_config.CharSwitch.ReturnOffsetX} px\n" +
                $"Offset Y dari Center: {_config.CharSwitch.ReturnOffsetY} px\n\n" +
                $"Pengaturan telah disimpan otomatis.",
                "Sukses Kalibrasi Return",
                WpfMessageBoxButton.OK,
                WpfMessageBoxImage.Information);
        }
    }

    private void BtnCalibrateStartGame_Click(object sender, RoutedEventArgs e)
    {
        var overlay = new CalibrationOverlay(
            _config.TargetWindowTitle,
            _config.GameName,
            "Di layar Pemilihan Karakter, KLIK 1 KALI tepat di tengah tombol START GAME.",
            showDefaultSuccessDialog: false);
        overlay.ShowDialog();

        if (overlay.IsSuccess)
        {
            _config.CharSwitch.StartGamePercentX = Math.Round(overlay.PercentX, 1);
            _config.CharSwitch.StartGamePercentY = Math.Round(overlay.PercentY, 1);
            TxtStartGamePercentX.Text = _config.CharSwitch.StartGamePercentX.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
            TxtStartGamePercentY.Text = _config.CharSwitch.StartGamePercentY.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);

            ConfigService.Save(_config);
            AddLog($"[INFO] Kalibrasi Start Game disimpan: Posisi = {_config.CharSwitch.StartGamePercentX:F1}% X, {_config.CharSwitch.StartGamePercentY:F1}% Y");

            WpfMessageBox.Show(
                $"Kalibrasi Tombol Start Game Berhasil!\n\n" +
                $"Posisi Horizontal: {_config.CharSwitch.StartGamePercentX:F1}%\n" +
                $"Posisi Vertikal: {_config.CharSwitch.StartGamePercentY:F1}%\n\n" +
                $"Pengaturan telah disimpan otomatis.",
                "Sukses Kalibrasi Start Game",
                WpfMessageBoxButton.OK,
                WpfMessageBoxImage.Information);
        }
    }

    private void BtnCalibrateCard1_Click(object sender, RoutedEventArgs e)
    {
        var overlay = new CalibrationOverlay(
            _config.TargetWindowTitle,
            _config.GameName,
            "Di layar Karakter, KLIK 1 KALI tepat di tengah HUD KARTU KARAKTER 1 (Atas).",
            showDefaultSuccessDialog: false);
        overlay.ShowDialog();

        if (overlay.IsSuccess)
        {
            _config.CharSwitch.Card1PercentX = Math.Round(overlay.PercentX, 1);
            _config.CharSwitch.Card1PercentY = Math.Round(overlay.PercentY, 1);
            TxtCard1PercentX.Text = _config.CharSwitch.Card1PercentX.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
            TxtCard1PercentY.Text = _config.CharSwitch.Card1PercentY.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);

            ConfigService.Save(_config);
            AddLog($"[INFO] Kalibrasi Card 1 disimpan: {_config.CharSwitch.Card1PercentX:F1}% X, {_config.CharSwitch.Card1PercentY:F1}% Y");
            WpfMessageBox.Show($"Kalibrasi Card 1 Berhasil!\n\nPosisi: {_config.CharSwitch.Card1PercentX:F1}% X, {_config.CharSwitch.Card1PercentY:F1}% Y\nPengaturan disimpan otomatis.", "Sukses Kalibrasi Card 1", WpfMessageBoxButton.OK, WpfMessageBoxImage.Information);
        }
    }

    private void BtnCalibrateCard2_Click(object sender, RoutedEventArgs e)
    {
        var overlay = new CalibrationOverlay(
            _config.TargetWindowTitle,
            _config.GameName,
            "Di layar Karakter, KLIK 1 KALI tepat di tengah HUD KARTU KARAKTER 2 (Tengah).",
            showDefaultSuccessDialog: false);
        overlay.ShowDialog();

        if (overlay.IsSuccess)
        {
            _config.CharSwitch.Card2PercentX = Math.Round(overlay.PercentX, 1);
            _config.CharSwitch.Card2PercentY = Math.Round(overlay.PercentY, 1);
            TxtCard2PercentX.Text = _config.CharSwitch.Card2PercentX.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
            TxtCard2PercentY.Text = _config.CharSwitch.Card2PercentY.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);

            ConfigService.Save(_config);
            AddLog($"[INFO] Kalibrasi Card 2 disimpan: {_config.CharSwitch.Card2PercentX:F1}% X, {_config.CharSwitch.Card2PercentY:F1}% Y");
            WpfMessageBox.Show($"Kalibrasi Card 2 Berhasil!\n\nPosisi: {_config.CharSwitch.Card2PercentX:F1}% X, {_config.CharSwitch.Card2PercentY:F1}% Y\nPengaturan disimpan otomatis.", "Sukses Kalibrasi Card 2", WpfMessageBoxButton.OK, WpfMessageBoxImage.Information);
        }
    }

    private void BtnCalibrateCard3_Click(object sender, RoutedEventArgs e)
    {
        var overlay = new CalibrationOverlay(
            _config.TargetWindowTitle,
            _config.GameName,
            "Di layar Karakter, KLIK 1 KALI tepat di tengah HUD KARTU KARAKTER 3 (Bawah).",
            showDefaultSuccessDialog: false);
        overlay.ShowDialog();

        if (overlay.IsSuccess)
        {
            _config.CharSwitch.Card3PercentX = Math.Round(overlay.PercentX, 1);
            _config.CharSwitch.Card3PercentY = Math.Round(overlay.PercentY, 1);
            TxtCard3PercentX.Text = _config.CharSwitch.Card3PercentX.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
            TxtCard3PercentY.Text = _config.CharSwitch.Card3PercentY.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);

            ConfigService.Save(_config);
            AddLog($"[INFO] Kalibrasi Card 3 disimpan: {_config.CharSwitch.Card3PercentX:F1}% X, {_config.CharSwitch.Card3PercentY:F1}% Y");
            WpfMessageBox.Show($"Kalibrasi Card 3 Berhasil!\n\nPosisi: {_config.CharSwitch.Card3PercentX:F1}% X, {_config.CharSwitch.Card3PercentY:F1}% Y\nPengaturan disimpan otomatis.", "Sukses Kalibrasi Card 3", WpfMessageBoxButton.OK, WpfMessageBoxImage.Information);
        }
    }

    private void BtnCalibratePagePrev_Click(object sender, RoutedEventArgs e)
    {
        var overlay = new CalibrationOverlay(
            _config.TargetWindowTitle,
            _config.GameName,
            "Di layar Karakter, KLIK 1 KALI tepat di tombol PANAH PREV ◀.",
            showDefaultSuccessDialog: false);
        overlay.ShowDialog();

        if (overlay.IsSuccess)
        {
            _config.CharSwitch.PagePrevPercentX = Math.Round(overlay.PercentX, 1);
            _config.CharSwitch.PagePrevPercentY = Math.Round(overlay.PercentY, 1);
            TxtPagePrevPercentX.Text = _config.CharSwitch.PagePrevPercentX.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
            TxtPagePrevPercentY.Text = _config.CharSwitch.PagePrevPercentY.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);

            ConfigService.Save(_config);
            AddLog($"[INFO] Kalibrasi Panah Prev ◀ disimpan: {_config.CharSwitch.PagePrevPercentX:F1}% X, {_config.CharSwitch.PagePrevPercentY:F1}% Y");
            WpfMessageBox.Show($"Kalibrasi Panah Prev ◀ Berhasil!\n\nPosisi: {_config.CharSwitch.PagePrevPercentX:F1}% X, {_config.CharSwitch.PagePrevPercentY:F1}% Y\nPengaturan disimpan otomatis.", "Sukses Kalibrasi Panah Prev ◀", WpfMessageBoxButton.OK, WpfMessageBoxImage.Information);
        }
    }

    private void BtnCalibratePageNext_Click(object sender, RoutedEventArgs e)
    {
        var overlay = new CalibrationOverlay(
            _config.TargetWindowTitle,
            _config.GameName,
            "Di layar Karakter, KLIK 1 KALI tepat di tombol PANAH NEXT ▶.",
            showDefaultSuccessDialog: false);
        overlay.ShowDialog();

        if (overlay.IsSuccess)
        {
            _config.CharSwitch.PageNextPercentX = Math.Round(overlay.PercentX, 1);
            _config.CharSwitch.PageNextPercentY = Math.Round(overlay.PercentY, 1);
            TxtPageNextPercentX.Text = _config.CharSwitch.PageNextPercentX.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
            TxtPageNextPercentY.Text = _config.CharSwitch.PageNextPercentY.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);

            ConfigService.Save(_config);
            AddLog($"[INFO] Kalibrasi Panah Next ▶ disimpan: {_config.CharSwitch.PageNextPercentX:F1}% X, {_config.CharSwitch.PageNextPercentY:F1}% Y");
            WpfMessageBox.Show($"Kalibrasi Panah Next ▶ Berhasil!\n\nPosisi: {_config.CharSwitch.PageNextPercentX:F1}% X, {_config.CharSwitch.PageNextPercentY:F1}% Y\nPengaturan disimpan otomatis.", "Sukses Kalibrasi Panah Next ▶", WpfMessageBoxButton.OK, WpfMessageBoxImage.Information);
        }
    }

    // ===================== KALIBRASI SPRITE MAGIC LAND (SML) =====================

    private void BtnCalibrateTabSml_Click(object sender, RoutedEventArgs e)
    {
        var overlay = new CalibrationOverlay(
            _config.TargetWindowTitle,
            _config.GameName,
            "Buka Dungeon Management, lalu KLIK 1 KALI tepat di Tab 'Sprite Magic Land'.",
            showDefaultSuccessDialog: false);
        overlay.ShowDialog();

        if (overlay.IsSuccess)
        {
            _config.Sml.TabSmlPercentX = Math.Round(overlay.PercentX, 1);
            _config.Sml.TabSmlPercentY = Math.Round(overlay.PercentY, 1);
            TxtTabSmlPercentX.Text = _config.Sml.TabSmlPercentX.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
            TxtTabSmlPercentY.Text = _config.Sml.TabSmlPercentY.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);

            ConfigService.Save(_config);
            AddLog($"[INFO] Kalibrasi Tab SML disimpan: {_config.Sml.TabSmlPercentX:F1}% X, {_config.Sml.TabSmlPercentY:F1}% Y");
            WpfMessageBox.Show($"Kalibrasi Tab SML Berhasil!\n\nPosisi: {_config.Sml.TabSmlPercentX:F1}% X, {_config.Sml.TabSmlPercentY:F1}% Y\nPengaturan disimpan otomatis.", "Sukses Kalibrasi Tab SML", WpfMessageBoxButton.OK, WpfMessageBoxImage.Information);
        }
    }

    private void BtnCalibrateSmlTower_Click(object sender, RoutedEventArgs e)
    {
        var overlay = new CalibrationOverlay(
            _config.TargetWindowTitle,
            _config.GameName,
            "Di tab SML, KLIK 1 KALI tepat di area TENGAH MENARA (posisi kursor untuk scroll mouse wheel).",
            showDefaultSuccessDialog: false);
        overlay.ShowDialog();

        if (overlay.IsSuccess)
        {
            _config.Sml.TowerCenterPercentX = Math.Round(overlay.PercentX, 1);
            _config.Sml.TowerCenterPercentY = Math.Round(overlay.PercentY, 1);
            TxtSmlTowerPercentX.Text = _config.Sml.TowerCenterPercentX.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
            TxtSmlTowerPercentY.Text = _config.Sml.TowerCenterPercentY.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);

            ConfigService.Save(_config);
            AddLog($"[INFO] Kalibrasi Area Menara SML disimpan: {_config.Sml.TowerCenterPercentX:F1}% X, {_config.Sml.TowerCenterPercentY:F1}% Y");
            WpfMessageBox.Show($"Kalibrasi Area Menara Berhasil!\n\nPosisi: {_config.Sml.TowerCenterPercentX:F1}% X, {_config.Sml.TowerCenterPercentY:F1}% Y\nPengaturan disimpan otomatis.", "Sukses Kalibrasi Menara SML", WpfMessageBoxButton.OK, WpfMessageBoxImage.Information);
        }
    }

    private void BtnCalibrateSmlEnter_Click(object sender, RoutedEventArgs e)
    {
        var overlay = new CalibrationOverlay(
            _config.TargetWindowTitle,
            _config.GameName,
            "Di tab SML, KLIK 1 KALI tepat di tombol 'Enter the...' (kanan bawah).",
            showDefaultSuccessDialog: false);
        overlay.ShowDialog();

        if (overlay.IsSuccess)
        {
            _config.Sml.EnterButtonPercentX = Math.Round(overlay.PercentX, 1);
            _config.Sml.EnterButtonPercentY = Math.Round(overlay.PercentY, 1);
            TxtSmlEnterPercentX.Text = _config.Sml.EnterButtonPercentX.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
            TxtSmlEnterPercentY.Text = _config.Sml.EnterButtonPercentY.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);

            ConfigService.Save(_config);
            AddLog($"[INFO] Kalibrasi Tombol Enter SML disimpan: {_config.Sml.EnterButtonPercentX:F1}% X, {_config.Sml.EnterButtonPercentY:F1}% Y");
            WpfMessageBox.Show($"Kalibrasi Tombol Enter Berhasil!\n\nPosisi: {_config.Sml.EnterButtonPercentX:F1}% X, {_config.Sml.EnterButtonPercentY:F1}% Y\nPengaturan disimpan otomatis.", "Sukses Kalibrasi Enter SML", WpfMessageBoxButton.OK, WpfMessageBoxImage.Information);
        }
    }

    private void BtnCalibrateSmlLv1_Click(object sender, RoutedEventArgs e) => CalibrateSmlLevel(1);
    private void BtnCalibrateSmlLv2_Click(object sender, RoutedEventArgs e) => CalibrateSmlLevel(2);
    private void BtnCalibrateSmlLv3_Click(object sender, RoutedEventArgs e) => CalibrateSmlLevel(3);
    private void BtnCalibrateSmlLv4_Click(object sender, RoutedEventArgs e) => CalibrateSmlLevel(4);

    private void CalibrateSmlLevel(int level)
    {
        var overlay = new CalibrationOverlay(
            _config.TargetWindowTitle,
            _config.GameName,
            $"Di tab SML, KLIK 1 KALI tepat di Card SML Level {level} di panel sebelah kiri.",
            showDefaultSuccessDialog: false);
        overlay.ShowDialog();

        if (overlay.IsSuccess)
        {
            double px = Math.Round(overlay.PercentX, 1);
            double py = Math.Round(overlay.PercentY, 1);

            switch (level)
            {
                case 1:
                    _config.Sml.Level1PercentX = px;
                    _config.Sml.Level1PercentY = py;
                    TxtSmlLv1PercentX.Text = px.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
                    TxtSmlLv1PercentY.Text = py.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
                    break;
                case 2:
                    _config.Sml.Level2PercentX = px;
                    _config.Sml.Level2PercentY = py;
                    TxtSmlLv2PercentX.Text = px.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
                    TxtSmlLv2PercentY.Text = py.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
                    break;
                case 3:
                    _config.Sml.Level3PercentX = px;
                    _config.Sml.Level3PercentY = py;
                    TxtSmlLv3PercentX.Text = px.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
                    TxtSmlLv3PercentY.Text = py.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
                    break;
                default:
                    _config.Sml.Level4PercentX = px;
                    _config.Sml.Level4PercentY = py;
                    TxtSmlLv4PercentX.Text = px.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
                    TxtSmlLv4PercentY.Text = py.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
                    break;
            }

            ConfigService.Save(_config);
            AddLog($"[INFO] Kalibrasi SML Level {level} disimpan: {px:F1}% X, {py:F1}% Y");
            WpfMessageBox.Show($"Kalibrasi SML Level {level} Berhasil!\n\nPosisi: {px:F1}% X, {py:F1}% Y\nPengaturan disimpan otomatis.", $"Sukses Kalibrasi SML Level {level}", WpfMessageBoxButton.OK, WpfMessageBoxImage.Information);
        }
    }

    private void BtnCalibrateSmlGate_Click(object sender, RoutedEventArgs e)
    {
        var overlay = new CalibrationOverlay(
            _config.TargetWindowTitle,
            _config.GameName,
            "Di tab SML, KLIK 1 KALI tepat di GERBANG LANTAI AKTIF di menara tengah.",
            showDefaultSuccessDialog: false);
        overlay.ShowDialog();

        if (overlay.IsSuccess)
        {
            _config.Sml.GatePercentX = Math.Round(overlay.PercentX, 1);
            _config.Sml.GatePercentY = Math.Round(overlay.PercentY, 1);
            TxtSmlGatePercentX.Text = _config.Sml.GatePercentX.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
            TxtSmlGatePercentY.Text = _config.Sml.GatePercentY.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);

            ConfigService.Save(_config);
            AddLog($"[INFO] Kalibrasi Gerbang Lantai SML disimpan: {_config.Sml.GatePercentX:F1}% X, {_config.Sml.GatePercentY:F1}% Y");
            WpfMessageBox.Show($"Kalibrasi Gerbang Lantai Berhasil!\n\nPosisi: {_config.Sml.GatePercentX:F1}% X, {_config.Sml.GatePercentY:F1}% Y\nPengaturan disimpan otomatis.", "Sukses Kalibrasi Gerbang SML", WpfMessageBoxButton.OK, WpfMessageBoxImage.Information);
        }
    }

    private void BtnShowSmlCalibrationOverlay_Click(object sender, RoutedEventArgs e)
    {
        int slotNum = (CmbPreviewSlotSml.SelectedIndex >= 0 ? CmbPreviewSlotSml.SelectedIndex : 0) + 1;
        var viewer = new CalibrationViewerOverlay(_config, _config.TargetWindowTitle, "sml", slotNum);
        viewer.ShowDialog();
    }

    private void BtnShowCharCalibrationOverlay_Click(object sender, RoutedEventArgs e)
    {
        int slotNum = (CmbPreviewSlotChar.SelectedIndex >= 0 ? CmbPreviewSlotChar.SelectedIndex : 0) + 1;
        var viewer = new CalibrationViewerOverlay(_config, _config.TargetWindowTitle, "charswitch", slotNum);
        viewer.ShowDialog();
    }

    private void BtnPreviewSmlSlot_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not WpfButton btn) return;
        if (!int.TryParse(btn.Tag?.ToString(), out int slotNum)) slotNum = 1;

        var viewer = new CalibrationViewerOverlay(_config, _config.TargetWindowTitle, "sml", slotNum);
        viewer.ShowDialog();
    }

    private void BtnPreviewCharSlot_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not WpfButton btn) return;
        if (!int.TryParse(btn.Tag?.ToString(), out int slotNum)) slotNum = 1;

        var viewer = new CalibrationViewerOverlay(_config, _config.TargetWindowTitle, "charswitch", slotNum);
        viewer.ShowDialog();
    }

    private void BtnTestTabSlot_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not WpfButton btn) return;
        if (!int.TryParse(btn.Tag?.ToString(), out int slotNum)) return;

        var slot = _slots[slotNum - 1];
        if (slot.WindowHandle == 0 || slot.Status == ClientStatus.Offline)
        {
            WpfMessageBox.Show(
                $"Slot {slotNum} belum mendeteksi jendela game / client offline.",
                LanguageService.Get("TitleWarn"),
                WpfMessageBoxButton.OK,
                WpfMessageBoxImage.Warning);
            return;
        }

        string mode = (CmbAutoTabMode.SelectedIndex == 1) ? "Foreground" : "Background";
        SendTabKey(slot.WindowHandle, mode);
        AddLog($"[TEST] Mengirim tombol TAB ke Slot {slotNum} ('{slot.WindowTitle}', PID: {slot.ProcessId}) menggunakan Mode {mode}.");
    }

    private void BtnStopAlarm_Click(object sender, RoutedEventArgs e)
    {
        _soundService.Stop();
        BtnStopAlarm.Visibility = Visibility.Collapsed;
        AddLog("[INFO] Alarm suara dihentikan / Sound alarm stopped.");
    }

    private void BtnBrowseSound_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new WpfOpenFileDialog
        {
            Filter = "Audio File WAV (*.wav)|*.wav|All Files (*.*)|*.*",
            Title = LanguageService.Get("TitleSoundDialog")
        };

        if (dialog.ShowDialog() == true)
        {
            TxtAudioPath.Text = dialog.FileName;
        }
    }

    private void BtnTestSound_Click(object sender, RoutedEventArgs e)
    {
        AddLog("[INFO] Memutar tes suara alarm / Testing sound alarm...");
        _soundService.TestSound(TxtAudioPath.Text);
    }

    private async void BtnTestDiscord_Click(object sender, RoutedEventArgs e)
    {
        var url = TxtDiscordWebhook.Text.Trim();
        if (string.IsNullOrWhiteSpace(url))
        {
            WpfMessageBox.Show(LanguageService.Get("MsgDiscordEmpty"), LanguageService.Get("TitleWarn"), WpfMessageBoxButton.OK, WpfMessageBoxImage.Warning);
            return;
        }

        AddLog("[INFO] Mengirim tes notifikasi Discord Webhook...");
        bool ok = await DiscordNotifier.SendTestAsync(url);
        if (ok)
        {
            // Simpan otomatis ke config agar tidak hilang jika lupa klik simpan
            _config.Discord.WebhookUrl = url;
            _config.Discord.Enabled = true;
            ChkDiscordEnabled.IsChecked = true;
            ConfigService.Save(_config);

            AddLog("[SUCCESS] " + LanguageService.Get("MsgDiscordSuccess"));
            WpfMessageBox.Show(LanguageService.Get("MsgDiscordSuccess"), LanguageService.Get("TitleSuccess"), WpfMessageBoxButton.OK, WpfMessageBoxImage.Information);
        }
        else
        {
            AddLog("[ERROR] " + LanguageService.Get("MsgDiscordFail"));
            WpfMessageBox.Show(LanguageService.Get("MsgDiscordFail"), LanguageService.Get("TitleError"), WpfMessageBoxButton.OK, WpfMessageBoxImage.Error);
        }
    }

    private async void BtnTestTelegram_Click(object sender, RoutedEventArgs e)
    {
        var token = TxtTelegramToken.Text.Trim();
        var chatId = TxtTelegramChatId.Text.Trim();

        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(chatId))
        {
            WpfMessageBox.Show(LanguageService.Get("MsgTelegramEmpty"), LanguageService.Get("TitleWarn"), WpfMessageBoxButton.OK, WpfMessageBoxImage.Warning);
            return;
        }

        AddLog("[INFO] Mengirim tes notifikasi Telegram...");
        bool ok = await TelegramNotifier.SendTestAsync(token, chatId);
        if (ok)
        {
            _config.Telegram.BotToken = token;
            _config.Telegram.ChatId = chatId;
            _config.Telegram.Enabled = true;
            ChkTelegramEnabled.IsChecked = true;
            ConfigService.Save(_config);

            AddLog("[SUCCESS] " + LanguageService.Get("MsgTelegramSuccess"));
            WpfMessageBox.Show(LanguageService.Get("MsgTelegramSuccess"), LanguageService.Get("TitleSuccess"), WpfMessageBoxButton.OK, WpfMessageBoxImage.Information);
        }
        else
        {
            AddLog("[ERROR] " + LanguageService.Get("MsgTelegramFail"));
            WpfMessageBox.Show(LanguageService.Get("MsgTelegramFail"), LanguageService.Get("TitleError"), WpfMessageBoxButton.OK, WpfMessageBoxImage.Error);
        }
    }

    private void BtnSaveSettings_Click(object sender, RoutedEventArgs e)
    {
        SaveSettingsFromUi(silent: false);
    }

    private void SaveSettingsFromUi(bool silent = false)
    {
        try
        {
            _config.Language = (CmbLanguage.SelectedIndex == 1) ? "en" : "id";
            LanguageService.CurrentLanguage = _config.Language;

            _config.ScanIntervalMs = int.TryParse(TxtScanInterval.Text, out var interval) ? Math.Max(500, interval) : 1500;
            _config.ConfirmationDelaySeconds = double.TryParse(TxtDelaySeconds.Text, out var delay) ? Math.Max(0.5, delay) : 1.5;
            _config.CooldownMinutes = int.TryParse(TxtCooldownMinutes.Text, out var cool) ? Math.Max(1, cool) : 3;
            _config.HpOffsetX = int.TryParse(TxtHpX.Text, out var x) ? x : 115;
            _config.HpOffsetY = int.TryParse(TxtHpY.Text, out var y) ? y : 42;
            _config.GameName = string.IsNullOrWhiteSpace(TxtGameName.Text) ? "Grand Fantasia" : TxtGameName.Text.Trim();
            _config.TargetWindowTitle = TxtTargetTitle.Text.Trim();
            _config.ShowHudBackground = ChkShowHudBackground.IsChecked ?? false;
            _config.EnableDebugLog = ChkEnableDebugLog.IsChecked ?? false;

            // Auto TAB
            _config.AutoTab.Enabled = ChkAutoTabEnabled.IsChecked ?? false;
            _config.AutoTab.SendMode = (CmbAutoTabMode.SelectedIndex == 1) ? "Foreground" : "Background";
            _config.AutoTab.IdleSeconds = double.TryParse(TxtAutoTabIdle.Text, out var idle) ? Math.Max(0.5, idle) : 3.0;
            _config.AutoTab.IntervalSeconds = double.TryParse(TxtAutoTabInterval.Text, out var intervalTab) ? Math.Max(0.5, intervalTab) : 1.5;
            _config.AutoTab.TargetOffsetX = int.TryParse(TxtAutoTabTargetX.Text, out var targetX) ? targetX : 470;
            _config.AutoTab.TargetOffsetY = int.TryParse(TxtAutoTabTargetY.Text, out var targetY) ? targetY : 35;

            _config.Discord.Enabled = ChkDiscordEnabled.IsChecked ?? false;
            _config.Discord.WebhookUrl = TxtDiscordWebhook.Text.Trim();
            _config.Discord.MentionEveryone = ChkDiscordMention.IsChecked ?? false;

            _config.Telegram.Enabled = ChkTelegramEnabled.IsChecked ?? false;
            _config.Telegram.InteractiveEnabled = ChkTelegramInteractive.IsChecked ?? true;
            _config.Telegram.BotToken = TxtTelegramToken.Text.Trim();
            _config.Telegram.ChatId = TxtTelegramChatId.Text.Trim();

            _config.SoundAlert.Enabled = ChkSoundEnabled.IsChecked ?? false;
            _config.SoundAlert.CustomAudioPath = TxtAudioPath.Text.Trim();

            _config.Storage.CropHudOnly = ChkCropHudOnly.IsChecked ?? true;

            // Character Switch Mapping
            _config.CharSwitch.LogoutOffsetY = int.TryParse(TxtLogoutOffsetY.Text, out var lY) ? lY : 78;
            _config.CharSwitch.ReturnOffsetX = int.TryParse(TxtReturnOffsetX.Text, out var rX) ? rX : -45;
            _config.CharSwitch.ReturnOffsetY = int.TryParse(TxtReturnOffsetY.Text, out var rY) ? rY : 32;
            _config.CharSwitch.StartGamePercentX = double.TryParse(TxtStartGamePercentX.Text, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var sgX) ? sgX : 50.0;
            _config.CharSwitch.StartGamePercentY = double.TryParse(TxtStartGamePercentY.Text, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var sgY) ? sgY : 98.1;

            _config.CharSwitch.Card1PercentX = double.TryParse(TxtCard1PercentX.Text, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var c1x) ? c1x : 85.4;
            _config.CharSwitch.Card1PercentY = double.TryParse(TxtCard1PercentY.Text, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var c1y) ? c1y : 17.1;
            _config.CharSwitch.Card2PercentX = double.TryParse(TxtCard2PercentX.Text, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var c2x) ? c2x : 85.4;
            _config.CharSwitch.Card2PercentY = double.TryParse(TxtCard2PercentY.Text, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var c2y) ? c2y : 39.4;
            _config.CharSwitch.Card3PercentX = double.TryParse(TxtCard3PercentX.Text, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var c3x) ? c3x : 85.4;
            _config.CharSwitch.Card3PercentY = double.TryParse(TxtCard3PercentY.Text, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var c3y) ? c3y : 61.8;

            _config.CharSwitch.PagePrevPercentX = double.TryParse(TxtPagePrevPercentX.Text, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var ppx) ? ppx : 73.8;
            _config.CharSwitch.PagePrevPercentY = double.TryParse(TxtPagePrevPercentY.Text, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var ppy) ? ppy : 77.2;
            _config.CharSwitch.PageNextPercentX = double.TryParse(TxtPageNextPercentX.Text, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var pnx) ? pnx : 84.4;
            _config.CharSwitch.PageNextPercentY = double.TryParse(TxtPageNextPercentY.Text, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var pny) ? pny : 77.2;

            // Clear SML (Sprite Magic Land)
            _config.Sml.DungeonHotkey = string.IsNullOrWhiteSpace(TxtSmlHotkey.Text) ? "," : TxtSmlHotkey.Text.Trim();
            _config.Sml.DungeonScanCode = Native.Win32.SCANCODE_COMMA;

            _config.Sml.TabSmlPercentX = double.TryParse(TxtTabSmlPercentX.Text, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var tsx) ? tsx : 48.5;
            _config.Sml.TabSmlPercentY = double.TryParse(TxtTabSmlPercentY.Text, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var tsy) ? tsy : 39.5;

            _config.Sml.Level1PercentX = double.TryParse(TxtSmlLv1PercentX.Text, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var l1x) ? l1x : 46.5;
            _config.Sml.Level1PercentY = double.TryParse(TxtSmlLv1PercentY.Text, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var l1y) ? l1y : 44.0;
            _config.Sml.Level2PercentX = double.TryParse(TxtSmlLv2PercentX.Text, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var l2x) ? l2x : 46.5;
            _config.Sml.Level2PercentY = double.TryParse(TxtSmlLv2PercentY.Text, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var l2y) ? l2y : 49.0;
            _config.Sml.Level3PercentX = double.TryParse(TxtSmlLv3PercentX.Text, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var l3x) ? l3x : 46.5;
            _config.Sml.Level3PercentY = double.TryParse(TxtSmlLv3PercentY.Text, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var l3y) ? l3y : 54.0;
            _config.Sml.Level4PercentX = double.TryParse(TxtSmlLv4PercentX.Text, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var l4x) ? l4x : 46.5;
            _config.Sml.Level4PercentY = double.TryParse(TxtSmlLv4PercentY.Text, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var l4y) ? l4y : 59.0;

            _config.Sml.TowerCenterPercentX = double.TryParse(TxtSmlTowerPercentX.Text, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var twx) ? twx : 61.5;
            _config.Sml.TowerCenterPercentY = double.TryParse(TxtSmlTowerPercentY.Text, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var twy) ? twy : 58.0;

            _config.Sml.GatePercentX = double.TryParse(TxtSmlGatePercentX.Text, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var gtx) ? gtx : 61.5;
            _config.Sml.GatePercentY = double.TryParse(TxtSmlGatePercentY.Text, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var gty) ? gty : 52.0;

            _config.Sml.EnterButtonPercentX = double.TryParse(TxtSmlEnterPercentX.Text, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var ebx) ? ebx : 86.5;
            _config.Sml.EnterButtonPercentY = double.TryParse(TxtSmlEnterPercentY.Text, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var eby) ? eby : 73.5;

            ConfigService.Save(_config);
            _scanTimer.Interval = TimeSpan.FromMilliseconds(_config.ScanIntervalMs);
            _storageService = new StorageCleanupService(_config.Storage.BaseFolder, _config.Storage.MaxDaysRetention, _config.Storage.MaxScreenshotsPerDay);
            ApplyLanguage();
            UpdateSlotUi();

            // Sinkronisasi HUD refresh timer dengan setting ShowHudBackground terbaru
            if (_config.ShowHudBackground)
                _hudRefreshTimer.Start();
            else
                _hudRefreshTimer.Stop();

            // Restart Telegram Listener dengan konfigurasi terbaru
            _telegramListener.Stop();
            _telegramListener.Start(_config);

            if (!silent)
            {
                AddLog("[SUCCESS] " + LanguageService.Get("MsgSaveSuccess"));
                WpfMessageBox.Show(LanguageService.Get("MsgSaveSuccess"), LanguageService.Get("TitleInfo"), WpfMessageBoxButton.OK, WpfMessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            if (!silent)
            {
                WpfMessageBox.Show($"Error: {ex.Message}", LanguageService.Get("TitleError"), WpfMessageBoxButton.OK, WpfMessageBoxImage.Error);
            }
        }
    }

    private void ChkShowHudBackground_Click(object sender, RoutedEventArgs e)
    {
        _config.ShowHudBackground = ChkShowHudBackground.IsChecked ?? false;

        if (_config.ShowHudBackground)
        {
            _hudRefreshTimer.Start();
            // Langsung refresh sekali supaya gambar langsung muncul tanpa menunggu 3 detik
            HudRefreshTimer_Tick(null, EventArgs.Empty);
        }
        else
        {
            _hudRefreshTimer.Stop();
        }

        UpdateSlotUi();
    }

    private async void BtnRunTestCommand_Click(object sender, RoutedEventArgs e)
    {
        string rawText = CmbTestCommand.Text?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(rawText))
        {
            AddLog("[WARN] Silakan pilih atau ketik perintah terlebih dahulu.");
            return;
        }

        AddLog($"[TEST-RUN] Menjalankan perintah: '{rawText}'...");

        string clean = rawText.Trim();
        if (clean.StartsWith('/'))
        {
            clean = clean.Substring(1).Trim();
        }

        var parts = clean.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return;

        string cmd = parts[0].ToLowerInvariant();

        try
        {
            BtnRunTestCommand.IsEnabled = false;

            // Handle SML command: sml_1_1_max, sml_1_1_90, sml_1_1_-2, sml 1 1 max, sml1 1 max, clearsml, dll.
            if (cmd.StartsWith("sml") || cmd.StartsWith("clearsml"))
            {
                int slot = 1;
                int smlLv = 1;
                string floorTarget = "max";

                var smlParts = clean.Split(new[] { ' ', '_' }, StringSplitOptions.RemoveEmptyEntries);
                if (smlParts.Length >= 4)
                {
                    int.TryParse(smlParts[1], out slot);
                    int.TryParse(smlParts[2], out smlLv);
                    floorTarget = smlParts[3];
                }
                else if (smlParts.Length == 3)
                {
                    if (smlParts[0].Length >= 4 && int.TryParse(smlParts[0].Substring(3, 1), out int sDigit))
                    {
                        slot = sDigit;
                        int.TryParse(smlParts[1], out smlLv);
                        floorTarget = smlParts[2];
                    }
                    else
                    {
                        int.TryParse(smlParts[1], out slot);
                        int.TryParse(smlParts[2], out smlLv);
                    }
                }
                else if (smlParts.Length == 2)
                {
                    if (smlParts[0].Length >= 4 && int.TryParse(smlParts[0].Substring(3, 1), out int sDigit))
                    {
                        slot = sDigit;
                        int.TryParse(smlParts[1], out smlLv);
                    }
                    else
                    {
                        int.TryParse(smlParts[1], out smlLv);
                    }
                }

                slot = Math.Clamp(slot, 1, 3);
                smlLv = Math.Clamp(smlLv, 1, 4);

                // Validasi slot aktif sebelum eksekusi
                var targetSlotEntry = _slots[slot - 1];
                if (targetSlotEntry.WindowHandle == 0 || targetSlotEntry.Status == ClientStatus.Offline)
                {
                    AddLog($"[WARN] ❌ Slot {slot} offline / jendela game tidak terdeteksi. Buka game terlebih dahulu.");
                    return;
                }

                if (_telegramListener.OnEnterSml != null)
                {
                    var (success, imagePath, msg) = await _telegramListener.OnEnterSml.Invoke(slot, smlLv, floorTarget);
                    AddLog($"[TEST-RESULT] Masuk SML Slot {slot} Lv {smlLv} Floor {floorTarget}: {(success ? "SUKSES" : "GAGAL")}");
                    if (!string.IsNullOrEmpty(msg)) AddLog($"[INFO] {msg}");
                }
                else
                {
                    AddLog("[ERROR] Handler OnEnterSml belum siap.");
                }
            }
            // Handle char switch: char2 1, char1 2, gantichar 1 2, char1, etc.
            else if (cmd.StartsWith("char") || cmd.StartsWith("gantichar") || (cmd.StartsWith("c") && char.IsDigit(cmd.Length > 1 ? cmd[1] : 'x')))
            {
                int slot = 1;
                int charNum = 1;

                if (cmd == "char" || cmd == "gantichar")
                {
                    if (parts.Length >= 3)
                    {
                        int.TryParse(parts[1], out slot);
                        int.TryParse(parts[2], out charNum);
                    }
                    else if (parts.Length == 2)
                    {
                        int.TryParse(parts[1], out charNum);
                    }
                }
                else if (cmd.StartsWith("char") && cmd.Length > 4 && int.TryParse(cmd.Substring(4, 1), out int sDigit))
                {
                    slot = sDigit;
                    if (parts.Length > 1) int.TryParse(parts[1], out charNum);
                }
                else if (cmd.StartsWith("c") && cmd.Length > 1 && int.TryParse(cmd.Substring(1, 1), out int cDigit))
                {
                    slot = cDigit;
                    if (parts.Length > 1) int.TryParse(parts[1], out charNum);
                }

                slot = Math.Clamp(slot, 1, 3);
                charNum = Math.Clamp(charNum, 1, 9);

                // Validasi slot aktif sebelum eksekusi
                var targetSlotChar = _slots[slot - 1];
                if (targetSlotChar.WindowHandle == 0 || targetSlotChar.Status == ClientStatus.Offline)
                {
                    AddLog($"[WARN] ❌ Slot {slot} offline / jendela game tidak terdeteksi. Buka game terlebih dahulu.");
                    return;
                }

                if (_telegramListener.OnSwitchCharacter != null)
                {
                    var (success, imagePath, msg) = await _telegramListener.OnSwitchCharacter.Invoke(slot, charNum);
                    AddLog($"[TEST-RESULT] Ganti Karakter Slot {slot} ke Char {charNum}: {(success ? "SUKSES" : "GAGAL")}");
                    if (!string.IsNullOrEmpty(msg)) AddLog($"[INFO] {msg}");
                }
                else
                {
                    AddLog("[ERROR] Handler OnSwitchCharacter belum siap.");
                }
            }
            else if (cmd.StartsWith("bag") || cmd.StartsWith("cektas") || cmd.StartsWith("tas"))
            {
                if (cmd == "allbag" || cmd == "cektasall")
                {
                    for (int s = 1; s <= 3; s++)
                    {
                        if (_telegramListener.OnCheckBackpack != null)
                        {
                            var (ok, img, msg) = await _telegramListener.OnCheckBackpack.Invoke(s);
                            AddLog($"[TEST-RESULT] Cek Tas Slot {s}: {(ok ? "SUKSES" : "GAGAL")} - {msg}");
                        }
                        if (s < 3) await Task.Delay(1000);
                    }
                }
                else
                {
                    int slot = 1;
                    if (parts.Length > 1 && int.TryParse(parts[1], out int pSlot)) slot = pSlot;
                    else if (cmd.Length > 3 && int.TryParse(cmd.Substring(cmd.Length - 1), out int endDigit)) slot = endDigit;

                    slot = Math.Clamp(slot, 1, 3);
                    if (_telegramListener.OnCheckBackpack != null)
                    {
                        var (ok, img, msg) = await _telegramListener.OnCheckBackpack.Invoke(slot);
                        AddLog($"[TEST-RESULT] Cek Tas Slot {slot}: {(ok ? "SUKSES" : "GAGAL")} - {msg}");
                    }
                }
            }
            else if (cmd == "tile")
            {
                int count = AutoTileService.TileGameWindows(_config.TargetWindowTitle);
                AddLog($"[TEST-RESULT] Berhasil merapikan {count} jendela game (Order by PID).");
            }
            else if (cmd == "status")
            {
                if (_telegramListener.OnGetStatus != null)
                {
                    string status = await _telegramListener.OnGetStatus.Invoke();
                    AddLog($"[TEST-RESULT] Status Realtime:\n{status}");
                }
            }
            else if (cmd == "ss" || cmd == "screenshot")
            {
                if (_telegramListener.OnTakeScreenshot != null)
                {
                    string? path = await _telegramListener.OnTakeScreenshot.Invoke();
                    AddLog($"[TEST-RESULT] Screenshot berhasil disimpan di: {path}");
                }
            }
            else if (cmd == "allsloton")
            {
                SetSlotMonitoring(1, true);
                SetSlotMonitoring(2, true);
                SetSlotMonitoring(3, true);
                AddLog("[TEST-RESULT] Monitoring SEMUA slot diaktifkan (ON).");
            }
            else if (cmd == "allslotoff")
            {
                SetSlotMonitoring(1, false);
                SetSlotMonitoring(2, false);
                SetSlotMonitoring(3, false);
                AddLog("[TEST-RESULT] Monitoring SEMUA slot dinonaktifkan (OFF).");
            }
            else if (cmd == "alltabon")
            {
                SetAutoTabSlot(1, true);
                SetAutoTabSlot(2, true);
                SetAutoTabSlot(3, true);
                AddLog("[TEST-RESULT] Auto TAB SEMUA slot diaktifkan (ON).");
            }
            else if (cmd == "alltaboff")
            {
                SetAutoTabSlot(1, false);
                SetAutoTabSlot(2, false);
                SetAutoTabSlot(3, false);
                AddLog("[TEST-RESULT] Auto TAB SEMUA slot dinonaktifkan (OFF).");
            }
            else if (cmd.StartsWith("tab") && cmd.Length >= 5)
            {
                int slot = int.Parse(cmd.Substring(3, 1));
                bool enable = cmd.EndsWith("on");
                SetAutoTabSlot(slot, enable);
                AddLog($"[TEST-RESULT] Auto TAB Slot {slot} diubah ke {(enable ? "ON" : "OFF")}.");
            }
            else if (cmd.StartsWith("slot") && cmd.Length >= 6)
            {
                int slot = int.Parse(cmd.Substring(4, 1));
                bool enable = cmd.EndsWith("on");
                SetSlotMonitoring(slot, enable);
                AddLog($"[TEST-RESULT] Monitoring Slot {slot} diubah ke {(enable ? "ON" : "OFF")}.");
            }
            else
            {
                AddLog($"[WARN] Perintah '{rawText}' tidak dikenali.");
            }
        }
        catch (Exception ex)
        {
            AddLog($"[ERROR] Eksekusi perintah test gagal: {ex.Message}");
        }
        finally
        {
            BtnRunTestCommand.IsEnabled = true;
        }
    }

    private void BtnClearLogs_Click(object sender, RoutedEventArgs e)
    {
        TxtLogs.Clear();
    }

    private async void ScanTimer_Tick(object? sender, EventArgs e)
    {
        var windows = WindowTrackerService.FindGameWindows(_config.TargetWindowTitle);

        for (int i = 0; i < 3; i++)
        {
            var slot = _slots[i];

            if (i < windows.Count)
            {
                var win = windows[i];
                slot.WindowHandle = win.Handle;
                slot.ProcessId = win.ProcessId;
                slot.WindowTitle = win.Title;

                // Skip full scan saat slot sedang dalam proses ganti karakter
                if (slot.Status == ClientStatus.Switching)
                {
                    continue;
                }

                if (!win.IsResponding)
                {
                    slot.Status = ClientStatus.NotResponding;
                    slot.LastInfo = "Game Not Responding (Freeze)";
                    continue;
                }

                // Ambil sample pixel bar HP
                var (color, hex) = PixelHealthScanner.SampleHpColor(win.Handle, _config.HpOffsetX, _config.HpOffsetY);
                slot.HealthColorHex = hex;

                bool isAlive = PixelHealthScanner.IsHpAlive(color);

                if (isAlive)
                {
                    bool wasNotAlive = (slot.Status != ClientStatus.Alive);
                    slot.DeadSince = null;
                    slot.Status = ClientStatus.Alive;
                    slot.LastInfo = $"{LanguageService.Get("HpNormal")} ({hex}) — {DateTime.Now:HH:mm:ss}";

                    if (wasNotAlive)
                    {
                        // Reset cooldown alert agar jika karakter mati lagi di kemudian saat, notifikasi langsung terpicu seketika
                        slot.LastAlertSentTime = null;
                        AddLog($"[INFO] Karakter Slot {slot.SlotIndex} ({slot.WindowTitle}) HIDUP / ALIVE ({hex}).");
                    }

                    // --- Auto Assist Target (Auto TAB) ---
                    if (_config.AutoTab.Enabled && slot.IsEnabled && slot.IsAutoTabEnabled)
                    {
                        var (targetColor, targetHex) = PixelHealthScanner.SampleTargetColor(win.Handle, _config.AutoTab.TargetOffsetX, _config.AutoTab.TargetOffsetY);
                        bool hasMonsterTarget = PixelHealthScanner.IsTargetMonsterPresent(targetColor, _config.AutoTab.TargetColorHex);
                        slot.HasTarget = hasMonsterTarget;

                        // Debug: log warna setiap 5 detik jika mode debug diaktifkan
                        if (_config.EnableDebugLog)
                        {
                            if (slot.LastTabSentTime == null || (DateTime.Now - slot.LastTabSentTime.Value).TotalSeconds >= 5)
                            {
                                AddLog($"[DEBUG] Slot {slot.SlotIndex}: Target pixel @({_config.AutoTab.TargetOffsetX},{_config.AutoTab.TargetOffsetY}) = {targetHex} | HasTarget={hasMonsterTarget}");
                            }
                        }

                        if (hasMonsterTarget)
                        {
                            slot.NoTargetSince = null;
                        }
                        else
                        {
                            if (slot.NoTargetSince == null)
                            {
                                slot.NoTargetSince = DateTime.Now;
                            }
                            else
                            {
                                var noTargetElapsed = (DateTime.Now - slot.NoTargetSince.Value).TotalSeconds;
                                bool intervalPassed = slot.LastTabSentTime == null ||
                                    (DateTime.Now - slot.LastTabSentTime.Value).TotalSeconds >= _config.AutoTab.IntervalSeconds;

                                if (noTargetElapsed >= _config.AutoTab.IdleSeconds && intervalPassed)
                                {
                                    slot.LastTabSentTime = DateTime.Now;
                                    SendTabKey(slot.WindowHandle, _config.AutoTab.SendMode);
                                    string logMsg = LanguageService.Format("LogAutoTabSent", noTargetElapsed, _config.AutoTab.SendMode);
                                    AddLog($"[ASSIST] Slot {slot.SlotIndex}: {logMsg}");
                                }
                            }
                        }
                    }
                    else
                    {
                        slot.NoTargetSince = null;
                        slot.LastTabSentTime = null;
                    }
                }
                else
                {
                    // Terdeteksi darah 0 / kosong
                    if (slot.DeadSince == null)
                    {
                        slot.DeadSince = DateTime.Now;
                        slot.LastInfo = $"{LanguageService.Get("VerifyingDelay")} ({_config.ConfirmationDelaySeconds:F1}s)";
                    }
                    else
                    {
                        var elapsed = (DateTime.Now - slot.DeadSince.Value).TotalSeconds;
                        if (elapsed >= _config.ConfirmationDelaySeconds)
                        {
                            slot.Status = ClientStatus.Dead;
                            slot.LastInfo = $"{LanguageService.Get("DeadConfirmed")} {elapsed:F1}s ({hex})";

                            // Cek cooldown notifikasi
                            bool canSendAlert = slot.IsEnabled &&
                                (slot.LastAlertSentTime == null ||
                                (DateTime.Now - slot.LastAlertSentTime.Value).TotalMinutes >= _config.CooldownMinutes);

                            if (canSendAlert)
                            {
                                slot.LastAlertSentTime = DateTime.Now;
                                await TriggerAlertAsync(slot.SlotIndex, slot.WindowTitle, slot.WindowHandle);
                            }
                            else if (!slot.IsEnabled)
                            {
                                slot.LastInfo = $"{LanguageService.Get("DeadNotifOff")} {elapsed:F1}s ({hex})";
                            }
                        }
                    }
                }
            }
            else
            {
                // Slot kosong / offline
                slot.WindowHandle = 0;
                slot.ProcessId = 0;
                slot.WindowTitle = LanguageService.Get("WaitingClient");
                slot.Status = ClientStatus.Offline;
                slot.HealthColorHex = "#333333";
                slot.LastInfo = LanguageService.Get("ClientNotDetected");
                slot.DeadSince = null;
                slot.LastAlertSentTime = null;
            }
        }

        // Auto-stop alarm jika karakter hidup kembali dan tidak ada slot yang sedang mati
        bool anyDead = _slots.Any(s => s.Status == ClientStatus.Dead);
        if (!anyDead && _soundService.IsPlaying)
        {
            _soundService.Stop();
            BtnStopAlarm.Visibility = Visibility.Collapsed;
            AddLog("[INFO] Karakter hidup kembali / Character revived. Alarm stop.");
        }

        UpdateSlotUi();
    }

    private async Task TriggerAlertAsync(int slotIndex, string windowTitle, nint hWnd)
    {
        AddLog($"[ALERT] PERINGATAN / ALERT: Slot {slotIndex} ({windowTitle}) DEAD!");

        // 1. Ambil screenshot jendela game (potongan bar darah / HUD)
        string savedImagePath = "";
        try
        {
            using var bmp = PixelHealthScanner.CaptureWindow(
                hWnd, 
                _config.Storage.CropHudOnly, 
                _config.Storage.CropWidth, 
                _config.Storage.CropHeight);

            if (bmp != null)
            {
                savedImagePath = _storageService.SaveScreenshot(bmp, slotIndex);
                AddLog($"[INFO] Screenshot HUD disimpan: {Path.GetFileName(savedImagePath)}");
            }
        }
        catch (Exception ex)
        {
            AddLog($"[WARN] Gagal menyimpan screenshot: {ex.Message}");
        }

        // 2. Mainkan alarm suara jika aktif
        if (_config.SoundAlert.Enabled)
        {
            _soundService.PlayLooping(_config.SoundAlert.CustomAudioPath);
            BtnStopAlarm.Visibility = Visibility.Visible;
            AddLog("[ALERT] Alarm suara berbunyi! / Alarm sounding!");
        }

        // 3. Kirim Discord Webhook jika aktif
        var webhookUrl = !string.IsNullOrWhiteSpace(_config.Discord.WebhookUrl)
            ? _config.Discord.WebhookUrl
            : TxtDiscordWebhook.Text.Trim();

        bool isDiscordActive = _config.Discord.Enabled || (ChkDiscordEnabled.IsChecked == true);

        if (isDiscordActive && !string.IsNullOrWhiteSpace(webhookUrl))
        {
            AddLog("[INFO] Mengirim alert ke Discord Webhook...");
            var (ok, msg) = await DiscordNotifier.SendAlertAsync(
                webhookUrl, 
                slotIndex, 
                windowTitle, 
                savedImagePath, 
                _config.Discord.MentionEveryone,
                _config.GameName);

            if (ok) AddLog($"[SUCCESS] Alert Discord: {msg}");
            else AddLog($"[ERROR] Gagal mengirim alert ke Discord: {msg}");
        }
        else if (!isDiscordActive)
        {
            AddLog("[INFO] Notifikasi Discord tidak dikirim (Checkbox dinonaktifkan).");
        }
        else
        {
            AddLog("[WARN] URL Webhook Discord kosong. Masukkan URL di tab Pengaturan.");
        }

        // 4. Kirim Telegram jika aktif
        var tgToken = !string.IsNullOrWhiteSpace(_config.Telegram.BotToken) ? _config.Telegram.BotToken : TxtTelegramToken.Text.Trim();
        var tgChatId = !string.IsNullOrWhiteSpace(_config.Telegram.ChatId) ? _config.Telegram.ChatId : TxtTelegramChatId.Text.Trim();
        bool isTgActive = _config.Telegram.Enabled || (ChkTelegramEnabled.IsChecked == true);

        if (isTgActive && !string.IsNullOrWhiteSpace(tgToken) && !string.IsNullOrWhiteSpace(tgChatId))
        {
            AddLog("[INFO] Mengirim alert ke Telegram Bot...");
            bool ok = await TelegramNotifier.SendAlertAsync(tgToken, tgChatId, slotIndex, windowTitle, savedImagePath, _config.GameName);
            if (ok) AddLog("[SUCCESS] Alert Telegram berhasil terkirim!");
            else AddLog("[ERROR] Gagal mengirim alert ke Telegram.");
        }
    }

    private void InitTelegramListener()
    {
        _telegramListener.OnLog = msg => AddLog(msg);

        _telegramListener.OnGetStatus = () =>
        {
            return Dispatcher.Invoke(() =>
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"📊 *Status {_config.GameName} Bot Monitor*");
                sb.AppendLine($"🕒 `{DateTime.Now:yyyy-MM-dd HH:mm:ss}`");
                sb.AppendLine($"⚡ Pemantauan: `{(_isMonitoring ? "AKTIF" : "NONAKTIF")}`\n");

                for (int i = 0; i < 3; i++)
                {
                    var s = _slots[i];
                    string statusIcon = s.Status == ClientStatus.Alive ? "🟢" : (s.Status == ClientStatus.Dead ? "🔴" : "⚪");
                    string slotStatus = s.IsEnabled ? "ON" : "OFF";
                    string tabStatus = s.IsAutoTabEnabled ? "ON" : "OFF";
                    sb.AppendLine($"{statusIcon} *Slot {s.SlotIndex}:* {s.StatusBadgeText} [Slot: {slotStatus} | TAB: {tabStatus}]");
                    if (s.ProcessId > 0)
                    {
                        sb.AppendLine($"   PID: `{s.ProcessId}` | Window: `{s.WindowTitle}`");
                    }
                    sb.AppendLine($"   Info: `{s.LastInfo}`");
                }
                return Task.FromResult(sb.ToString());
            });
        };

        _telegramListener.OnTakeScreenshot = () =>
        {
            return Dispatcher.Invoke(async () =>
            {
                try
                {
                    string dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Capture", "Remote");
                    Directory.CreateDirectory(dir);
                    string path = Path.Combine(dir, $"ss_{DateTime.Now:yyyyMMdd_HHmmss}.png");

                    int screenWidth = (int)SystemParameters.PrimaryScreenWidth;
                    int screenHeight = (int)SystemParameters.PrimaryScreenHeight;

                    using var bmp = new System.Drawing.Bitmap(screenWidth, screenHeight);
                    using (var g = System.Drawing.Graphics.FromImage(bmp))
                    {
                        g.CopyFromScreen(0, 0, 0, 0, new System.Drawing.Size(screenWidth, screenHeight));
                    }
                    bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
                    return await Task.FromResult<string?>(path);
                }
                catch (Exception ex)
                {
                    AddLog($"[ERROR] Remote screenshot gagal: {ex.Message}");
                    return null;
                }
            });
        };

        _telegramListener.OnTileWindows = () =>
        {
            return Dispatcher.Invoke(() =>
            {
                try
                {
                    int count = AutoTileService.TileGameWindows(_config.TargetWindowTitle);
                    AddLog($"[REMOTE] AutoTile dipanggil via Telegram. Merapikan {count} jendela.");
                    return Task.FromResult(count > 0);
                }
                catch
                {
                    return Task.FromResult(false);
                }
            });
        };

        _telegramListener.OnToggleMonitoring = (start) =>
        {
            return Dispatcher.Invoke(() =>
            {
                if (start && !_isMonitoring)
                {
                    BtnToggleMonitoring_Click(BtnToggleMonitoring, new RoutedEventArgs());
                    return Task.FromResult("▶️ Pemantauan bot berhasil DINYALAKAN.");
                }
                else if (!start && _isMonitoring)
                {
                    BtnToggleMonitoring_Click(BtnToggleMonitoring, new RoutedEventArgs());
                    return Task.FromResult("⏹️ Pemantauan bot berhasil DIMATIKAN.");
                }
                else
                {
                    string state = _isMonitoring ? "sudah AKTIF" : "sudah NONAKTIF";
                    return Task.FromResult($"ℹ️ Status pemantauan saat ini {state}.");
                }
            });
        };

        _telegramListener.OnCheckBackpack = async (slotNum) =>
        {
            return await Dispatcher.Invoke(async () =>
            {
                int idx = slotNum - 1;
                if (idx < 0 || idx >= _slots.Count)
                {
                    return (false, null, $"❌ Nomor slot {slotNum} tidak valid (Gunakan 1-3).");
                }

                var slot = _slots[idx];
                if (slot.WindowHandle == 0 || slot.Status == ClientStatus.Offline)
                {
                    return (false, null, $"❌ Slot {slotNum} offline / jendela tidak ditemukan.");
                }

                return await BackpackInspectorService.InspectBackpackAsync(
                    slot.WindowHandle, 
                    slotNum, 
                    slot.WindowTitle, 
                    (hWnd, sc, hold) => SendHardwareKey(hWnd, sc, hold));
            });
        };

        _telegramListener.OnSwitchCharacter = (slotNum, charNum) =>
        {
            return Dispatcher.Invoke(async () =>
            {
                int idx = slotNum - 1;
                if (idx < 0 || idx >= _slots.Count)
                {
                    return (false, null, $"❌ Slot {slotNum} tidak valid.");
                }

                var slot = _slots[idx];
                if (slot.WindowHandle == 0 || slot.Status == ClientStatus.Offline)
                {
                    return (false, null, $"❌ Slot {slotNum} offline / jendela tidak ditemukan.");
                }

                bool wasAutoTabEnabled = slot.IsAutoTabEnabled;
                if (wasAutoTabEnabled)
                {
                    SetAutoTabSlot(slotNum, false);
                    AddLog($"[REMOTE] Auto TAB Slot {slotNum} di-pause sementara untuk pergantian karakter.");
                }

                // Set status SWITCHING agar badge di kartu slot berubah menjadi ungu
                var previousStatus = slot.Status;
                slot.Status = ClientStatus.Switching;
                slot.LastInfo = $"⟳ Proses ganti karakter ke nomor {charNum}...";
                UpdateSlotUi();

                AddLog($"[REMOTE] Memulai ganti karakter Slot {slotNum} ke Karakter {charNum}...");

                var result = await CharacterSwitcherService.SwitchCharacterAsync(
                    slot.WindowHandle,
                    slotNum,
                    charNum,
                    slot.WindowTitle,
                    (hWnd, sc, hold) => SendHardwareKey(hWnd, sc, hold),
                    () =>
                    {
                        var (color, _) = PixelHealthScanner.SampleHpColor(slot.WindowHandle, _config.HpOffsetX, _config.HpOffsetY);
                        return PixelHealthScanner.IsHpAlive(color);
                    },
                    _config.CharSwitch);

                // Pulihkan status berdasarkan hasil switch
                slot.Status = result.success ? ClientStatus.Alive : previousStatus;
                UpdateSlotUi();

                if (wasAutoTabEnabled)
                {
                    SetAutoTabSlot(slotNum, true);
                    AddLog($"[REMOTE] Auto TAB Slot {slotNum} dipulihkan (ON).");
                }

                AddLog($"[REMOTE] Hasil pergantian karakter Slot {slotNum}: {(result.success ? "SUKSES" : "GAGAL")}");
                return result;
            });
        };

        _telegramListener.OnEnterSml = (slotNum, smlLv, floorTarget) =>
        {
            return Dispatcher.Invoke(async () =>
            {
                int idx = slotNum - 1;
                if (idx < 0 || idx >= _slots.Count)
                {
                    return (false, null, $"❌ Slot {slotNum} tidak valid.");
                }

                var slot = _slots[idx];
                if (slot.WindowHandle == 0 || slot.Status == ClientStatus.Offline)
                {
                    return (false, null, $"❌ Slot {slotNum} offline / jendela tidak ditemukan.");
                }

                bool wasAutoTabEnabled = slot.IsAutoTabEnabled;
                if (wasAutoTabEnabled)
                {
                    SetAutoTabSlot(slotNum, false);
                    AddLog($"[REMOTE] Auto TAB Slot {slotNum} di-pause sementara untuk masuk SML.");
                }

                var previousStatus = slot.Status;
                slot.Status = ClientStatus.Switching;
                slot.LastInfo = $"🏰 Masuk SML Lv {smlLv} ({floorTarget})...";
                UpdateSlotUi();

                AddLog($"[REMOTE] Memulai masuk SML Slot {slotNum}: Level {smlLv}, Floor {floorTarget}...");

                var result = await SmlService.EnterSmlAsync(
                    slot.WindowHandle,
                    slotNum,
                    smlLv,
                    floorTarget,
                    slot.WindowTitle,
                    (hWnd, sc, hold) => SendHardwareKey(hWnd, sc, hold),
                    () =>
                    {
                        var (color, _) = PixelHealthScanner.SampleHpColor(slot.WindowHandle, _config.HpOffsetX, _config.HpOffsetY);
                        return PixelHealthScanner.IsHpAlive(color);
                    },
                    _config.Sml,
                    msg => AddLog(msg));

                slot.Status = result.success ? ClientStatus.Alive : previousStatus;
                UpdateSlotUi();

                if (wasAutoTabEnabled)
                {
                    SetAutoTabSlot(slotNum, true);
                    AddLog($"[REMOTE] Auto TAB Slot {slotNum} dipulihkan (ON).");
                }

                AddLog($"[REMOTE] Hasil masuk SML Slot {slotNum}: {(result.success ? "SUKSES" : "GAGAL")}");
                return result;
            });
        };

        _telegramListener.OnToggleSlot = (slotNum, enable) =>
        {
            return Dispatcher.Invoke(() =>
            {
                bool ok = SetSlotMonitoring(slotNum, enable);
                if (ok)
                {
                    string status = enable ? "🟢 AKTIF (ON)" : "⚪ NONAKTIF (OFF)";
                    return Task.FromResult($"🔔 Monitoring Slot {slotNum} berhasil diubah ke {status}.");
                }
                return Task.FromResult($"❌ Gagal mengubah status Slot {slotNum}.");
            });
        };

        _telegramListener.OnToggleAutoTab = (slotNum, enable) =>
        {
            return Dispatcher.Invoke(() =>
            {
                bool ok = SetAutoTabSlot(slotNum, enable);
                if (ok)
                {
                    string status = enable ? "🟢 AKTIF (ON)" : "⚪ NONAKTIF (OFF)";
                    return Task.FromResult($"🎯 Auto TAB Slot {slotNum} berhasil diubah ke {status}.");
                }
                return Task.FromResult($"❌ Gagal mengubah Auto TAB Slot {slotNum}.");
            });
        };

        _telegramListener.Start(_config);
    }

    protected override void OnClosed(EventArgs e)
    {
        _telegramListener.Stop();
        _scanTimer.Stop();
        _soundService.Stop();
        _notifyIcon?.Dispose();
        base.OnClosed(e);
        System.Windows.Application.Current.Shutdown();
    }
}
