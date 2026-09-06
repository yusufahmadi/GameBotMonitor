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

    public DiscordConfig Discord { get; set; } = new();
    public TelegramConfig Telegram { get; set; } = new();
    public SoundConfig SoundAlert { get; set; } = new();
    public StorageConfig Storage { get; set; } = new();
    public AutoTabConfig AutoTab { get; set; } = new();
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
    public int CropWidth { get; set; } = 300;
    public int CropHeight { get; set; } = 140;
}
