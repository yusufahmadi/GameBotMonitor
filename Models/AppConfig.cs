namespace GameBotMonitor.Models;

public class AppConfig
{
    public int ScanIntervalMs { get; set; } = 1500;
    public double ConfirmationDelaySeconds { get; set; } = 1.5;
    public int HpOffsetX { get; set; } = 115;
    public int HpOffsetY { get; set; } = 42;
    public string GameName { get; set; } = "Grand Fantasia";
    public string TargetWindowTitle { get; set; } = "Grand Fantasia Origin";
    public int CooldownMinutes { get; set; } = 3;
    public string Language { get; set; } = "id"; // "id" or "en"
    public bool ShowHudBackground { get; set; } = false;
    public bool EnableDebugLog { get; set; } = false;

    public DiscordConfig Discord { get; set; } = new();
    public TelegramConfig Telegram { get; set; } = new();
    public SoundConfig SoundAlert { get; set; } = new();
    public StorageConfig Storage { get; set; } = new();
    public AutoTabConfig AutoTab { get; set; } = new();
    public CharacterSwitchConfig CharSwitch { get; set; } = new();
}

public class CharacterSwitchConfig
{
    public int LogoutOffsetX { get; set; } = 0;
    public int LogoutOffsetY { get; set; } = 78; // Y offset dari center window ke tombol Logout (di bawah Channel)
    public int ReturnOffsetX { get; set; } = -45; // X offset dari center window ke tombol Return (bypass countdown 10s)
    public int ReturnOffsetY { get; set; } = 32;  // Y offset dari center window ke tombol Return
    public double StartGamePercentX { get; set; } = 50.0; // Persentase X tombol Start Game di bawah
    public double StartGamePercentY { get; set; } = 98.1; // Persentase Y tombol Start Game di bawah

    // HUD Kartu Karakter 1, 2, 3 di sebelah kanan
    public double Card1PercentX { get; set; } = 85.4;
    public double Card1PercentY { get; set; } = 17.1;
    public double Card2PercentX { get; set; } = 85.4;
    public double Card2PercentY { get; set; } = 39.4;
    public double Card3PercentX { get; set; } = 85.4;
    public double Card3PercentY { get; set; } = 61.8;

    // Tombol Navigasi Halaman Karakter (Prev ◀ & Next ▶)
    public double PagePrevPercentX { get; set; } = 73.8;
    public double PagePrevPercentY { get; set; } = 77.2;
    public double PageNextPercentX { get; set; } = 84.4;
    public double PageNextPercentY { get; set; } = 77.2;
}

public class AutoTabConfig
{
    public bool Enabled { get; set; } = true;
    public string SendMode { get; set; } = "Background"; // "Background" or "Foreground"
    public double IdleSeconds { get; set; } = 3.0;
    public double IntervalSeconds { get; set; } = 1.5;
    public int TargetOffsetX { get; set; } = 470; // Perkiraan tengah atas (frame target monster)
    public int TargetOffsetY { get; set; } = 35;
    public string TargetColorHex { get; set; } = ""; // Referensi warna saat ada monster (opsional)
}

public class DiscordConfig
{
    public bool Enabled { get; set; } = true;
    public string WebhookUrl { get; set; } = "";
    public bool MentionEveryone { get; set; } = true;
    public bool SendScreenshot { get; set; } = true;
}

public class TelegramConfig
{
    public bool Enabled { get; set; } = false;
    public string BotToken { get; set; } = "";
    public string ChatId { get; set; } = "";
    public bool SendScreenshot { get; set; } = true;
    public bool InteractiveEnabled { get; set; } = true;
    public double PollingIntervalSeconds { get; set; } = 2.0;
}

public class SoundConfig
{
    public bool Enabled { get; set; } = true;
    public string CustomAudioPath { get; set; } = "";
    public int Volume { get; set; } = 100;
}

public class StorageConfig
{
    public string BaseFolder { get; set; } = "Capture";
    public int MaxDaysRetention { get; set; } = 3;
    public int MaxScreenshotsPerDay { get; set; } = 10;
    public bool CropHudOnly { get; set; } = true;
    public int CropWidth { get; set; } = 225;
    public int CropHeight { get; set; } = 92;
}
