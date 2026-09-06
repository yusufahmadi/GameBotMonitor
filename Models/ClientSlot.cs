using System.ComponentModel;
using System.Runtime.CompilerServices;
using GameBotMonitor.Services;

namespace GameBotMonitor.Models;

public class ClientSlot : INotifyPropertyChanged
{
    public int SlotIndex { get; set; }
    public nint WindowHandle { get; set; }
    public int ProcessId { get; set; }

    private bool _isEnabled = true;
    public bool IsEnabled
    {
        get => _isEnabled;
        set { _isEnabled = value; OnPropertyChanged(); OnPropertyChanged(nameof(StatusBadgeBackground)); }
    }

    private string _windowTitle = "Menunggu Client...";
    public string WindowTitle
    {
        get => _windowTitle;
        set { _windowTitle = value; OnPropertyChanged(); }
    }

    private ClientStatus _status = ClientStatus.Offline;
    public ClientStatus Status
    {
        get => _status;
        set
        {
            if (_status != value)
            {
                _status = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(StatusBadgeText));
                OnPropertyChanged(nameof(StatusBadgeBackground));
            }
        }
    }

    private string _healthColorHex = "#333333";
    public string HealthColorHex
    {
        get => _healthColorHex;
        set { _healthColorHex = value; OnPropertyChanged(); }
    }

    private string _lastInfo = "Tidak aktif";
    public string LastInfo
    {
        get => _lastInfo;
        set { _lastInfo = value; OnPropertyChanged(); }
    }

    public DateTime? DeadSince { get; set; }
    public DateTime? LastAlertSentTime { get; set; }

    // Auto Assist Target (Auto TAB) State
    private bool _isAutoTabEnabled = false;
    public bool IsAutoTabEnabled
    {
        get => _isAutoTabEnabled;
        set { _isAutoTabEnabled = value; OnPropertyChanged(); }
    }
    public DateTime? NoTargetSince { get; set; }
    public DateTime? LastTabSentTime { get; set; }
    public bool HasTarget { get; set; } = false;

    public string StatusBadgeText => Status switch
    {
        ClientStatus.Alive => LanguageService.Get("BadgeAlive"),
        ClientStatus.Dead => LanguageService.Get("BadgeDead"),
        ClientStatus.Crashed => LanguageService.Get("BadgeCrashed"),
        ClientStatus.NotResponding => LanguageService.Get("BadgeNotResponding"),
        _ => LanguageService.Get("BadgeOffline")
    };

    public string StatusBadgeBackground => Status switch
    {
        ClientStatus.Alive => "#2E7D32",         // Green
        ClientStatus.Dead => "#C62828",          // Red
        ClientStatus.Crashed => "#E65100",       // Dark Orange
        ClientStatus.NotResponding => "#F57F17",  // Amber
        _ => "#424242"                           // Gray
    };

    public event PropertyChangedEventHandler? PropertyChanged;
    public void RefreshLanguage()
    {
        OnPropertyChanged(nameof(StatusBadgeText));
    }
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
