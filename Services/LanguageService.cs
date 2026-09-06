namespace GameBotMonitor.Services;

public static class LanguageService
{
    public static string CurrentLanguage { get; set; } = "id";

    private static readonly Dictionary<string, Dictionary<string, string>> Translations = new()
    {
        ["id"] = new()
        {
            // Header & Toolbar
            ["AppTitle"] = "{0} Bot Health Monitor",
            ["AppSubtitle"] = "Pemantau Otomatis Darah Karakter & Notifikasi Multi-Client (.NET 8)",
            ["BtnStartMonitor"] = "\u25B6 Mulai Pantau",
            ["BtnStopMonitor"] = "\u25A0 Berhenti Pantau",
            ["BtnAutoTile"] = "\u25A6 Rapikan Jendela",
            ["BtnCalibrate"] = "\u2316 Kalibrasi HP",
            ["BtnStopAlarm"] = "\u25A0 Stop Alarm",

            // Slot Cards
            ["Slot1Title"] = "SLOT 1 (Kiri)",
            ["Slot2Title"] = "SLOT 2 (Tengah)",
            ["Slot3Title"] = "SLOT 3 (Kanan)",
            ["WaitingClient"] = "Menunggu Client...",
            ["HpColorSample"] = "Sample Warna Bar HP:",
            ["NoScanYet"] = "Belum ada pemindaian",
            ["ClientNotDetected"] = "Client tidak terdeteksi",
            ["HpNormal"] = "Darah Normal",
            ["VerifyingDelay"] = "Darah 0 terdeteksi... verifikasi delay",
            ["DeadConfirmed"] = "MATI! Terkonfirmasi",
            ["DeadNotifOff"] = "MATI! (Notifikasi OFF)",

            // Status Badges
            ["BadgeAlive"] = "\u25CF NORMAL / HIDUP",
            ["BadgeDead"] = "\u25A0 DARAH 0 / MATI",
            ["BadgeCrashed"] = "\u25B2 CLIENT CRASH / CLOSED",
            ["BadgeNotResponding"] = "\u23F3 NOT RESPONDING",
            ["BadgeOffline"] = "\u25CB OFFLINE",

            // Tabs
            ["TabLogs"] = "Log Aktivitas",
            ["TabSettings"] = "Pengaturan & Notifikasi",
            ["BtnClearLogs"] = "Hapus Log",

            // Settings - Language
            ["SecLanguage"] = "Bahasa / Language",
            ["LblLanguage"] = "Pilih Bahasa Aplikasi:",

            // Settings - Detection
            ["SecDetection"] = "Pengaturan Deteksi & Delay",
            ["LblScanInterval"] = "Interval Pindai Layar (ms):",
            ["HintScanInterval"] = "Rekomendasi: 1000 - 2000 ms (default 1500)",
            ["LblDelaySeconds"] = "Delay Konfirmasi Kematian (detik):",
            ["HintDelaySeconds"] = "Waktu tunggu sebelum alert dikirim (default 1.5s)",
            ["LblCooldownMinutes"] = "Cooldown Notifikasi Ulang (menit):",
            ["HintCooldownMinutes"] = "Jeda anti-spam setelah alert dikirim",
            ["LblHpCoords"] = "Koordinat Relatif HP Bar (X, Y):",
            ["HintHpCoords"] = "Bisa otomatis diisi via tombol 'Kalibrasi HP'",
            ["LblGameName"] = "Nama Game (Display Name):",
            ["HintGameName"] = "Nama game untuk judul aplikasi & notifikasi",
            ["LblTargetTitle"] = "Target Judul Window Game:",
            ["HintTargetTitle"] = "Kata kunci judul jendela game yang dicari",
            ["ChkShowHudBackground"] = "Tampilkan cuplikan info karakter sebagai background kartu slot",
            ["HintShowHudBackground"] = "Menampilkan gambar live HUD karakter (avatar & bar HP) di latar belakang kartu saat aktif",

            // Settings - Discord
            ["SecDiscord"] = "Discord Webhook",
            ["ChkDiscord"] = "Aktifkan Notifikasi Discord",
            ["ChkMention"] = "Mention @everyone (Bunyikan HP)",
            ["BtnTestDiscord"] = "Test Discord",

            // Settings - Telegram
            ["SecTelegram"] = "Telegram Bot",
            ["ChkTelegram"] = "Aktifkan Notifikasi Telegram",
            ["LblTelegramToken"] = "Telegram Bot Token:",
            ["LblTelegramChatId"] = "Chat ID Penerima:",
            ["BtnTestTelegram"] = "Test Telegram",

            // Settings - Sound
            ["SecSound"] = "Alarm Suara Lokal PC",
            ["ChkSound"] = "Aktifkan Alarm Suara di Speaker Komputer",
            ["BtnBrowseSound"] = "Pilih File .wav",
            ["BtnTestSound"] = "Test Suara",
            ["HintSound"] = "*Kosongkan untuk memakai suara sirine peringatan bawaan otomatis.",

            // Settings - Storage
            ["SecStorage"] = "Tangkapan Layar & Pembersihan (Auto-Cleanup):",
            ["ChkCropHud"] = "Cukup kirim potongan bar darah / HUD karakter saja (Ukuran: 300x140 px)",
            ["StorageInfo1"] = "• Tangkapan layar otomatis disimpan ke: Capture/yyyy/MM/dd/",
            ["StorageInfo2"] = "• Otomatis menyimpan hanya 3 hari terakhir (folder lebih lama langsung dibersihkan)",
            ["StorageInfo3"] = "• Di setiap folder harian, maksimal tersimpan 10 tangkapan layar terakhir",
            ["BtnSaveSettings"] = "Simpan Semua Pengaturan",

            // System Tray
            ["TrayOpenApp"] = "Buka Aplikasi",
            ["TrayExit"] = "Keluar",
            ["TrayBalloonTitle"] = "Bot Health Monitor",
            ["TrayBalloonText"] = "Aplikasi diminimalkan ke System Tray. Klik 2x ikon untuk membuka kembali.",

            // Dialogs & Messages
            ["MsgSaveSuccess"] = "Semua pengaturan berhasil disimpan!",
            ["MsgDiscordEmpty"] = "Masukkan URL Webhook Discord terlebih dahulu!",
            ["MsgDiscordSuccess"] = "Tes Webhook Discord berhasil terkirim dan tersimpan otomatis!",
            ["MsgDiscordFail"] = "Gagal mengirim ke Webhook Discord. Periksa URL dan koneksi internet Anda.",
            ["MsgTelegramEmpty"] = "Masukkan Bot Token dan Chat ID Telegram terlebih dahulu!",
            ["MsgTelegramSuccess"] = "Tes Telegram berhasil terkirim dan tersimpan otomatis!",
            ["MsgTelegramFail"] = "Gagal mengirim pesan Telegram. Pastikan Bot Token & Chat ID valid.",
            ["TitleInfo"] = "Informasi",
            ["TitleWarn"] = "Peringatan",
            ["TitleSuccess"] = "Sukses",
            ["TitleError"] = "Gagal",
            ["TitleSoundDialog"] = "Pilih File Suara Alarm",

            // Slot Toggle Log
            ["SlotEnabledLog"] = "diaktifkan. Pemantauan dan notifikasi slot ini kembali ON.",
            ["SlotDisabledLog"] = "dinonaktifkan. Notifikasi slot ini tidak akan dikirim.",

            // Discord & Telegram Notifications
            ["DiscordAlertTitle"] = "🚨 PERINGATAN: Karakter Terdeteksi Mati! (Slot {0})",
            ["DiscordAlertDesc"] = "Karakter di **Slot {0}** ({1}) telah mati / darahnya 0!",
            ["DiscordMentionMsg"] = "@everyone Karakter bot game Anda telah mati atau darahnya 0!",
            ["DiscordFieldSlot"] = "🎮 Slot",
            ["DiscordFieldWindow"] = "🖥️ Jendela",
            ["DiscordFieldTime"] = "⏰ Waktu Kejadian",
            ["DiscordFieldStatus"] = "⚠️ Status",
            ["DiscordStatusDead"] = "Darah 0 (Mati)",
            ["DiscordTestContent"] = "✅ **Tes Notifikasi Berhasil!** Bot Health Monitor terkoneksi dengan baik ke server Discord ini.",
            ["DiscordTestTitle"] = "Uji Coba Koneksi Webhook",
            ["DiscordTestDesc"] = "Notifikasi Discord siap digunakan untuk memantau status bot.",
            ["TelegramAlertCaption"] = "🚨 *PERINGATAN: Karakter Terdeteksi Mati!*\n\n🎮 *Slot:* Slot {0}\n🖥️ *Jendela:* {1}\n⏰ *Waktu:* {2}\n⚠️ *Status:* Darah 0 (Mati)",
            ["TelegramTestMsg"] = "✅ *Tes Notifikasi Berhasil!*\nBot Health Monitor terhubung ke Telegram ini dan siap mengirim peringatan."
        },
        ["en"] = new()
        {
            // Header & Toolbar
            ["AppTitle"] = "{0} Bot Health Monitor",
            ["AppSubtitle"] = "Automatic Character Health Monitor & Multi-Client Alerts (.NET 8)",
            ["BtnStartMonitor"] = "\u25B6 Start Monitor",
            ["BtnStopMonitor"] = "\u25A0 Stop Monitor",
            ["BtnAutoTile"] = "\u25A6 Tile Windows",
            ["BtnCalibrate"] = "\u2316 Calibrate HP",
            ["BtnStopAlarm"] = "\u25A0 Stop Alarm",

            // Slot Cards
            ["Slot1Title"] = "SLOT 1 (Left)",
            ["Slot2Title"] = "SLOT 2 (Center)",
            ["Slot3Title"] = "SLOT 3 (Right)",
            ["WaitingClient"] = "Waiting for Client...",
            ["HpColorSample"] = "HP Bar Color Sample:",
            ["NoScanYet"] = "No scans yet",
            ["ClientNotDetected"] = "Client not detected",
            ["HpNormal"] = "Normal Health",
            ["VerifyingDelay"] = "Zero HP detected... verifying delay",
            ["DeadConfirmed"] = "DEAD! Confirmed",
            ["DeadNotifOff"] = "DEAD! (Notification OFF)",

            // Status Badges
            ["BadgeAlive"] = "\u25CF NORMAL / ALIVE",
            ["BadgeDead"] = "\u25A0 HP 0 / DEAD",
            ["BadgeCrashed"] = "\u25B2 CLIENT CRASH / CLOSED",
            ["BadgeNotResponding"] = "\u23F3 NOT RESPONDING",
            ["BadgeOffline"] = "\u25CB OFFLINE",

            // Tabs
            ["TabLogs"] = "Activity Logs",
            ["TabSettings"] = "Settings & Notifications",
            ["BtnClearLogs"] = "Clear Logs",

            // Settings - Language
            ["SecLanguage"] = "Language / Bahasa",
            ["LblLanguage"] = "Select Application Language:",

            // Settings - Detection
            ["SecDetection"] = "Detection & Delay Settings",
            ["LblScanInterval"] = "Screen Scan Interval (ms):",
            ["HintScanInterval"] = "Recommended: 1000 - 2000 ms (default 1500)",
            ["LblDelaySeconds"] = "Death Confirmation Delay (sec):",
            ["HintDelaySeconds"] = "Wait time before sending alert (default 1.5s)",
            ["LblCooldownMinutes"] = "Notification Cooldown (minutes):",
            ["HintCooldownMinutes"] = "Anti-spam delay after alert is sent",
            ["LblHpCoords"] = "HP Bar Relative Coordinates (X, Y):",
            ["HintHpCoords"] = "Can be filled via 'Calibrate HP' button",
            ["LblGameName"] = "Game Name (Display Name):",
            ["HintGameName"] = "Game name for app title & alerts",
            ["LblTargetTitle"] = "Target Game Window Title:",
            ["HintTargetTitle"] = "Keyword for target game window title",
            ["ChkShowHudBackground"] = "Show character info snapshot as slot card background",
            ["HintShowHudBackground"] = "Displays live character HUD (avatar & HP bar) as the card background when active",

            // Settings - Discord
            ["SecDiscord"] = "Discord Webhook",
            ["ChkDiscord"] = "Enable Discord Notifications",
            ["ChkMention"] = "Mention @everyone (Trigger Sound)",
            ["BtnTestDiscord"] = "Test Discord",

            // Settings - Telegram
            ["SecTelegram"] = "Telegram Bot",
            ["ChkTelegram"] = "Enable Telegram Notifications",
            ["LblTelegramToken"] = "Telegram Bot Token:",
            ["LblTelegramChatId"] = "Recipient Chat ID:",
            ["BtnTestTelegram"] = "Test Telegram",

            // Settings - Sound
            ["SecSound"] = "PC Local Sound Alarm",
            ["ChkSound"] = "Enable Sound Alarm on PC Speakers",
            ["BtnBrowseSound"] = "Select .wav File",
            ["BtnTestSound"] = "Test Sound",
            ["HintSound"] = "*Leave empty to use built-in warning siren automatically.",

            // Settings - Storage
            ["SecStorage"] = "Screenshots & Storage Cleanup:",
            ["ChkCropHud"] = "Only send cropped character HP bar / HUD (Size: 300x140 px)",
            ["StorageInfo1"] = "• Screenshots automatically saved to: Capture/yyyy/MM/dd/",
            ["StorageInfo2"] = "• Retains only the last 3 days (older folders auto-cleaned)",
            ["StorageInfo3"] = "• Each daily folder retains a maximum of 10 screenshots",
            ["BtnSaveSettings"] = "Save All Settings",

            // System Tray
            ["TrayOpenApp"] = "Open Application",
            ["TrayExit"] = "Exit",
            ["TrayBalloonTitle"] = "Bot Health Monitor",
            ["TrayBalloonText"] = "Application minimized to System Tray. Double-click icon to restore.",

            // Dialogs & Messages
            ["MsgSaveSuccess"] = "All settings saved successfully!",
            ["MsgDiscordEmpty"] = "Please enter Discord Webhook URL first!",
            ["MsgDiscordSuccess"] = "Discord Webhook test sent and saved successfully!",
            ["MsgDiscordFail"] = "Failed to send to Discord Webhook. Check your URL and internet connection.",
            ["MsgTelegramEmpty"] = "Please enter Telegram Bot Token and Chat ID first!",
            ["MsgTelegramSuccess"] = "Telegram test sent and saved successfully!",
            ["MsgTelegramFail"] = "Failed to send Telegram message. Check Bot Token & Chat ID.",
            ["TitleInfo"] = "Information",
            ["TitleWarn"] = "Warning",
            ["TitleSuccess"] = "Success",
            ["TitleError"] = "Failed",
            ["TitleSoundDialog"] = "Select Alarm Sound File",

            // Slot Toggle Log
            ["SlotEnabledLog"] = "enabled. Monitoring and alerts for this slot are ON.",
            ["SlotDisabledLog"] = "disabled. Alerts for this slot will not be sent.",

            // Discord & Telegram Notifications
            ["DiscordAlertTitle"] = "🚨 WARNING: Character Detected Dead! (Slot {0})",
            ["DiscordAlertDesc"] = "Character in **Slot {0}** ({1}) has died / HP is 0!",
            ["DiscordMentionMsg"] = "@everyone Your game bot character has died or HP is 0!",
            ["DiscordFieldSlot"] = "🎮 Slot",
            ["DiscordFieldWindow"] = "🖥️ Window",
            ["DiscordFieldTime"] = "⏰ Timestamp",
            ["DiscordFieldStatus"] = "⚠️ Status",
            ["DiscordStatusDead"] = "0 HP (Dead)",
            ["DiscordTestContent"] = "✅ **Notification Test Successful!** Bot Health Monitor is connected to this Discord server.",
            ["DiscordTestTitle"] = "Webhook Connection Test",
            ["DiscordTestDesc"] = "Discord notifications are ready to monitor bot status.",
            ["TelegramAlertCaption"] = "🚨 *WARNING: Character Detected Dead!*\n\n🎮 *Slot:* Slot {0}\n🖥️ *Window:* {1}\n⏰ *Time:* {2}\n⚠️ *Status:* 0 HP (Dead)",
            ["TelegramTestMsg"] = "✅ *Notification Test Successful!*\nBot Health Monitor is connected to this Telegram and ready to send alerts."
        }
    };

    public static string Get(string key)
    {
        string lang = CurrentLanguage.ToLowerInvariant() == "en" ? "en" : "id";
        if (Translations.TryGetValue(lang, out var dict) && dict.TryGetValue(key, out var val))
        {
            return val;
        }

        // Fallback to id
        if (Translations["id"].TryGetValue(key, out var fallbackVal))
        {
            return fallbackVal;
        }

        return key;
    }

    public static string Format(string key, params object[] args)
    {
        string template = Get(key);
        try
        {
            return string.Format(template, args);
        }
        catch
        {
            return template;
        }
    }
}
