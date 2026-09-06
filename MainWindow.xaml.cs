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
    private readonly SoundAlertService _soundService = new();
    private StorageCleanupService _storageService;
    private readonly List<ClientSlot> _slots = new();
    private bool _isMonitoring = false;

    // System Tray
    private System.Windows.Forms.NotifyIcon _notifyIcon = null!;
    private System.Windows.Forms.ToolStripMenuItem _trayItemOpen = null!;
    private System.Windows.Forms.ToolStripMenuItem _trayItemExit = null!;
    private bool _closeFromTray = false;

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

        // Inisialisasi bahasa dari config
        LanguageService.CurrentLanguage = _config.Language ?? "id";

        // System Tray
        InitNotifyIcon();
        this.StateChanged += MainWindow_StateChanged;

        LoadConfigToUi();
        ApplyLanguage();
        UpdateSlotUi();

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
        TxtHpSample1.Text = LanguageService.Get("HpColorSample");
        TxtHpSample2.Text = LanguageService.Get("HpColorSample");
        TxtHpSample3.Text = LanguageService.Get("HpColorSample");

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

    private void BtnToggleSlot_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not WpfButton btn) return;
        if (!int.TryParse(btn.Tag?.ToString(), out int slotNum)) return;

        var slot = _slots[slotNum - 1];
        slot.IsEnabled = !slot.IsEnabled;

        if (slot.IsEnabled)
        {
            btn.Content = "\u25CF ON";
            btn.Background = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#2E7D32"));
            // Reset cooldown agar bisa kirim notifikasi langsung saat diaktifkan
            slot.LastAlertSentTime = null;
            slot.DeadSince = null;
            AddLog($"[INFO] Slot {slotNum} {LanguageService.Get("SlotEnabledLog")}");
        }
        else
        {
            btn.Content = "\u25CB OFF";
            btn.Background = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#6C7086"));
            AddLog($"[INFO] Slot {slotNum} {LanguageService.Get("SlotDisabledLog")}");
        }
    }

    // ===================== AUTO TAB TOGGLE PER SLOT =====================

    private void BtnToggleAutoTabSlot_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not WpfButton btn) return;
        if (!int.TryParse(btn.Tag?.ToString(), out int slotNum)) return;

        var slot = _slots[slotNum - 1];
        slot.IsAutoTabEnabled = !slot.IsAutoTabEnabled;

        if (slot.IsAutoTabEnabled)
        {
            btn.Content = "● ON";
            btn.Background = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#1E88E5"));
            btn.Foreground = System.Windows.Media.Brushes.White;
            slot.NoTargetSince = null;
            slot.LastTabSentTime = null;
            AddLog($"[INFO] Slot {slotNum}: Auto TAB diaktifkan (ON).");
        }
        else
        {
            btn.Content = "○ OFF";
            btn.Background = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#45475A"));
            btn.Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#CDD6F4"));
            slot.NoTargetSince = null;
            slot.LastTabSentTime = null;
            AddLog($"[INFO] Slot {slotNum}: Auto TAB dinonaktifkan (OFF).");
        }
    }

    public static void SendTabKey(nint hWnd, string mode)
    {
        try
        {
            if (hWnd == 0) return;

            if (mode == "Foreground")
            {
                // Mode B: Bawa jendela ke depan lalu kirim SendInput dengan SCAN CODE
                // DirectInput games memerlukan hardware scan code (0x0F = TAB), bukan virtual key
                Native.Win32.SetForegroundWindow(hWnd);
                System.Threading.Thread.Sleep(100);

                int structSize = System.Runtime.InteropServices.Marshal.SizeOf(typeof(Native.Win32.INPUT));

                // 1. KeyDown TAB — gunakan scan code 0x0F dan flag KEYEVENTF_SCANCODE
                var inputDown = new Native.Win32.INPUT[1];
                inputDown[0] = new Native.Win32.INPUT
                {
                    type = Native.Win32.INPUT_KEYBOARD,
                    u = new Native.Win32.InputUnion
                    {
                        ki = new Native.Win32.KEYBDINPUT
                        {
                            wVk = 0,                                       // 0 = pakai scan code
                            wScan = 0x0F,                                  // Hardware scan code untuk TAB
                            dwFlags = Native.Win32.KEYEVENTF_SCANCODE,    // Mode scan code
                            time = 0,
                            dwExtraInfo = IntPtr.Zero
                        }
                    }
                };
                //Native.Win32.SendInput(1, inputDown, structSize);
                uint resultDown = Native.Win32.SendInput(1, inputDown, structSize);
                if (resultDown == 0)
                {
                    int error = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
                    System.Diagnostics.Debug.WriteLine($"SendInput gagal dengan kode error: {error}");
                }


                // Tahan tombol selama 50ms agar terdeteksi oleh render/game loop
                // Update 150
                System.Threading.Thread.Sleep(150);

                // 2. KeyUp TAB
                var inputUp = new Native.Win32.INPUT[1];
                inputUp[0] = new Native.Win32.INPUT
                {
                    type = Native.Win32.INPUT_KEYBOARD,
                    u = new Native.Win32.InputUnion
                    {
                        ki = new Native.Win32.KEYBDINPUT
                        {
                            wVk = 0,
                            wScan = 0x0F,
                            dwFlags = Native.Win32.KEYEVENTF_SCANCODE | Native.Win32.KEYEVENTF_KEYUP,
                            time = 0,
                            dwExtraInfo = IntPtr.Zero
                        }
                    }
                };
                //Native.Win32.SendInput(1, inputUp, structSize);
                uint resultUp = Native.Win32.SendInput(1, inputUp, structSize);
                if (resultUp == 0)
                {
                    int error = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
                    System.Diagnostics.Debug.WriteLine($"SendInput gagal dengan kode error: {error}");
                }
            }
            else
            {
                // Mode A (Background): Kirim WM_CHAR + WM_KEYDOWN/WM_KEYUP ke message queue jendela game
                // WM_CHAR sebagai prioritas (beberapa game merespons WM_CHAR lebih dahulu)
                nint wParam = (nint)Native.Win32.VK_TAB;
                nint lParamDown = (nint)0x000F0001; // Scan code 0x0F, repeat 1
                nint lParamUp = unchecked((nint)0xC00F0001); // Bit 30+31 = KeyUp

                // Kirim WM_KEYDOWN + WM_KEYUP (standard)
                Native.Win32.PostMessage(hWnd, Native.Win32.WM_KEYDOWN, wParam, lParamDown);
                System.Threading.Thread.Sleep(20);
                Native.Win32.PostMessage(hWnd, Native.Win32.WM_KEYUP, wParam, lParamUp);
                System.Threading.Thread.Sleep(20);
                // Kirim WM_CHAR sebagai fallback (engine seperti Grand Fantasia kadang hanya merespons WM_CHAR)
                Native.Win32.PostMessage(hWnd, Native.Win32.WM_CHAR, wParam, lParamDown);

                //// Kirim WM_KEYDOWN
                //Native.Win32.PostMessage(hWnd, Native.Win32.WM_KEYDOWN, wParam, lParamDown);
                //System.Threading.Thread.Sleep(20);

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
        TxtTelegramToken.Text = _config.Telegram.BotToken;
        TxtTelegramChatId.Text = _config.Telegram.ChatId;

        ChkSoundEnabled.IsChecked = _config.SoundAlert.Enabled;
        TxtAudioPath.Text = _config.SoundAlert.CustomAudioPath;

        ChkCropHudOnly.IsChecked = _config.Storage.CropHudOnly;
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

        // Tampilan Background HUD Karakter (Opsional)
        if (_config.ShowHudBackground && slot.Status != ClientStatus.Offline && slot.WindowHandle != 0)
        {
            try
            {
                using var bmp = PixelHealthScanner.CaptureWindow(slot.WindowHandle, true, 300, 140);
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
            _config.Telegram.BotToken = TxtTelegramToken.Text.Trim();
            _config.Telegram.ChatId = TxtTelegramChatId.Text.Trim();

            _config.SoundAlert.Enabled = ChkSoundEnabled.IsChecked ?? false;
            _config.SoundAlert.CustomAudioPath = TxtAudioPath.Text.Trim();

            _config.Storage.CropHudOnly = ChkCropHudOnly.IsChecked ?? true;

            ConfigService.Save(_config);
            _scanTimer.Interval = TimeSpan.FromMilliseconds(_config.ScanIntervalMs);
            _storageService = new StorageCleanupService(_config.Storage.BaseFolder, _config.Storage.MaxDaysRetention, _config.Storage.MaxScreenshotsPerDay);
            ApplyLanguage();

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
                    slot.LastInfo = $"{LanguageService.Get("HpNormal")} ({hex})";

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

    protected override void OnClosed(EventArgs e)
    {
        _scanTimer.Stop();
        _soundService.Stop();
        _notifyIcon?.Dispose();
        base.OnClosed(e);
        System.Windows.Application.Current.Shutdown();
    }
}
