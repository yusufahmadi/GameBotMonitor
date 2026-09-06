using System.Windows;
using System.Windows.Input;
using GameBotMonitor.Native;
using GameBotMonitor.Services;

namespace GameBotMonitor.Views;

public partial class CalibrationOverlay : Window
{
    public int ResultOffsetX { get; private set; } = -1;
    public int ResultOffsetY { get; private set; } = -1;
    public bool IsSuccess { get; private set; }

    private readonly string _targetKeyword;
    private readonly string _gameName;

    public CalibrationOverlay(string targetKeyword = "Grand Fantasia", string gameName = "Grand Fantasia")
    {
        InitializeComponent();
        _targetKeyword = targetKeyword;
        _gameName = string.IsNullOrWhiteSpace(gameName) ? targetKeyword : gameName;
        TxtInstruction.Text = $"Arahkan kursor dan KLIK 1 KALI tepat di bar darah jendela game {_gameName}.";
    }

    private void Window_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            var screenPoint = PointToScreen(e.GetPosition(this));
            int clickX = (int)screenPoint.X;
            int clickY = (int)screenPoint.Y;

            // Cari jendela game yang berada di bawah posisi klik mouse
            var windows = WindowTrackerService.FindGameWindows(_targetKeyword);
            GameWindowEntry? matchedWindow = null;

            foreach (var win in windows)
            {
                if (clickX >= win.Rect.Left && clickX <= win.Rect.Right &&
                    clickY >= win.Rect.Top && clickY <= win.Rect.Bottom)
                {
                    matchedWindow = win;
                    break;
                }
            }

            if (matchedWindow != null)
            {
                ResultOffsetX = clickX - matchedWindow.Rect.Left;
                ResultOffsetY = clickY - matchedWindow.Rect.Top;
                IsSuccess = true;

                WpfMessageBox.Show(
                    $"Kalibrasi Berhasil!\n\n" +
                    $"Jendela: {matchedWindow.Title}\n" +
                    $"Offset X: {ResultOffsetX}\n" +
                    $"Offset Y: {ResultOffsetY}\n\n" +
                    $"Koordinat relatif ini telah disimpan ke konfigurasi.",
                    "Sukses Kalibrasi",
                    WpfMessageBoxButton.OK,
                    WpfMessageBoxImage.Information
                );
            }
            else
            {
                WpfMessageBox.Show(
                    $"Tidak mendeteksi jendela game {_gameName} di titik klik ({clickX}, {clickY}).\n" +
                    $"Pastikan jendela game terlihat di layar.",
                    "Kalibrasi Gagal",
                    WpfMessageBoxButton.OK,
                    WpfMessageBoxImage.Warning
                );
            }

            Close();
        }
    }

    private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
        }
    }
}
