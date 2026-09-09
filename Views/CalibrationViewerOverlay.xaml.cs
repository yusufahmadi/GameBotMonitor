using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using GameBotMonitor.Models;
using GameBotMonitor.Native;
using GameBotMonitor.Services;
using MediaColor = System.Windows.Media.Color;
using WpfKeyEventArgs = System.Windows.Input.KeyEventArgs;
using WpfKey = System.Windows.Input.Key;
using WpfMouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;

namespace GameBotMonitor.Views;

public partial class CalibrationViewerOverlay : Window
{
    public CalibrationViewerOverlay(AppConfig config, string targetTitle, string mode = "all", int slotNum = 1)
    {
        InitializeComponent();

        Loaded += (s, e) =>
        {
            SetupOverlay(config, targetTitle, mode, slotNum);
        };
    }

    private void SetupOverlay(AppConfig config, string targetTitle, string mode, int slotNum)
    {
        var windows = WindowTrackerService.FindGameWindows(targetTitle);
        string winTitle = targetTitle;

        if (windows.Count == 0)
        {
            Left = 0;
            Top = 0;
            Width = SystemParameters.PrimaryScreenWidth;
            Height = SystemParameters.PrimaryScreenHeight;
        }
        else
        {
            int targetIdx = Math.Clamp(slotNum - 1, 0, windows.Count - 1);
            var win = windows[targetIdx];
            winTitle = win.Title;
            Left = win.Rect.Left;
            Top = win.Rect.Top;
            Width = win.Rect.Width;
            Height = win.Rect.Height;
        }

        string modeName = mode switch
        {
            "sml" => "SPRITE MAGIC LAND (SML)",
            "charswitch" => "GANTI KARAKTER",
            _ => "SEMUA KALIBRASI"
        };

        TxtTitleHeader.Text = $"👁️ POSISI KALIBRASI {modeName} — SLOT {slotNum} ({winTitle})";

        OverlayCanvas.Children.Clear();

        double w = Width;
        double h = Height;

        // ==================== 1. TITIK SML ====================
        if (mode == "all" || mode == "sml")
        {
            AddPin(w * (config.Sml.TabSmlPercentX / 100.0), h * (config.Sml.TabSmlPercentY / 100.0),
                "Tab SML", $"{config.Sml.TabSmlPercentX:F1}%, {config.Sml.TabSmlPercentY:F1}%", MediaColor.FromRgb(137, 180, 250));

            AddPin(w * (config.Sml.Level1PercentX / 100.0), h * (config.Sml.Level1PercentY / 100.0),
                "SML Lv 1", $"{config.Sml.Level1PercentX:F1}%, {config.Sml.Level1PercentY:F1}%", MediaColor.FromRgb(203, 166, 247));
            AddPin(w * (config.Sml.Level2PercentX / 100.0), h * (config.Sml.Level2PercentY / 100.0),
                "SML Lv 2", $"{config.Sml.Level2PercentX:F1}%, {config.Sml.Level2PercentY:F1}%", MediaColor.FromRgb(203, 166, 247));
            AddPin(w * (config.Sml.Level3PercentX / 100.0), h * (config.Sml.Level3PercentY / 100.0),
                "SML Lv 3", $"{config.Sml.Level3PercentX:F1}%, {config.Sml.Level3PercentY:F1}%", MediaColor.FromRgb(203, 166, 247));
            AddPin(w * (config.Sml.Level4PercentX / 100.0), h * (config.Sml.Level4PercentY / 100.0),
                "SML Lv 4", $"{config.Sml.Level4PercentX:F1}%, {config.Sml.Level4PercentY:F1}%", MediaColor.FromRgb(203, 166, 247));

            AddPin(w * (config.Sml.TowerCenterPercentX / 100.0), h * (config.Sml.TowerCenterPercentY / 100.0),
                "Area Menara (Scroll)", $"{config.Sml.TowerCenterPercentX:F1}%, {config.Sml.TowerCenterPercentY:F1}%", MediaColor.FromRgb(250, 179, 135));

            AddPin(w * (config.Sml.GatePercentX / 100.0), h * (config.Sml.GatePercentY / 100.0),
                "Gerbang Lantai (Pilih)", $"{config.Sml.GatePercentX:F1}%, {config.Sml.GatePercentY:F1}%", MediaColor.FromRgb(245, 194, 231));

            AddPin(w * (config.Sml.EnterButtonPercentX / 100.0), h * (config.Sml.EnterButtonPercentY / 100.0),
                "Tombol Enter", $"{config.Sml.EnterButtonPercentX:F1}%, {config.Sml.EnterButtonPercentY:F1}%", MediaColor.FromRgb(166, 227, 161));
        }

        // ==================== 2. TITIK CHARACTER SWITCH ====================
        if (mode == "all" || mode == "charswitch")
        {
            double cx = w / 2.0;
            double cy = h / 2.0;

            AddPin(cx + config.CharSwitch.LogoutOffsetX, cy + config.CharSwitch.LogoutOffsetY,
                "Tombol Logout", $"(Center + {config.CharSwitch.LogoutOffsetX}, +{config.CharSwitch.LogoutOffsetY})", MediaColor.FromRgb(243, 139, 168));

            AddPin(cx + config.CharSwitch.ReturnOffsetX, cy + config.CharSwitch.ReturnOffsetY,
                "Tombol Return (Countdown)", $"(Center + {config.CharSwitch.ReturnOffsetX}, +{config.CharSwitch.ReturnOffsetY})", MediaColor.FromRgb(249, 226, 175));

            AddPin(w * (config.CharSwitch.StartGamePercentX / 100.0), h * (config.CharSwitch.StartGamePercentY / 100.0),
                "Start Game", $"{config.CharSwitch.StartGamePercentX:F1}%, {config.CharSwitch.StartGamePercentY:F1}%", MediaColor.FromRgb(148, 226, 213));

            AddPin(w * (config.CharSwitch.Card1PercentX / 100.0), h * (config.CharSwitch.Card1PercentY / 100.0),
                "Kartu Karakter 1", $"{config.CharSwitch.Card1PercentX:F1}%, {config.CharSwitch.Card1PercentY:F1}%", MediaColor.FromRgb(180, 190, 254));
            AddPin(w * (config.CharSwitch.Card2PercentX / 100.0), h * (config.CharSwitch.Card2PercentY / 100.0),
                "Kartu Karakter 2", $"{config.CharSwitch.Card2PercentX:F1}%, {config.CharSwitch.Card2PercentY:F1}%", MediaColor.FromRgb(180, 190, 254));
            AddPin(w * (config.CharSwitch.Card3PercentX / 100.0), h * (config.CharSwitch.Card3PercentY / 100.0),
                "Kartu Karakter 3", $"{config.CharSwitch.Card3PercentX:F1}%, {config.CharSwitch.Card3PercentY:F1}%", MediaColor.FromRgb(180, 190, 254));

            AddPin(w * (config.CharSwitch.PagePrevPercentX / 100.0), h * (config.CharSwitch.PagePrevPercentY / 100.0),
                "Panah Prev ◀", $"{config.CharSwitch.PagePrevPercentX:F1}%, {config.CharSwitch.PagePrevPercentY:F1}%", MediaColor.FromRgb(166, 209, 137));
            AddPin(w * (config.CharSwitch.PageNextPercentX / 100.0), h * (config.CharSwitch.PageNextPercentY / 100.0),
                "Panah Next ▶", $"{config.CharSwitch.PageNextPercentX:F1}%, {config.CharSwitch.PageNextPercentY:F1}%", MediaColor.FromRgb(166, 209, 137));
        }

        // ==================== 3. TITIK BAR HP & TARGET MONSTER ====================
        if (mode == "all")
        {
            AddPin(config.HpOffsetX, config.HpOffsetY,
                "Sample Bar HP", $"({config.HpOffsetX}, {config.HpOffsetY}) px", MediaColor.FromRgb(166, 227, 161));

            if (config.AutoTab.Enabled)
            {
                AddPin(config.AutoTab.TargetOffsetX, config.AutoTab.TargetOffsetY,
                    "Target Monster Bar", $"({config.AutoTab.TargetOffsetX}, {config.AutoTab.TargetOffsetY}) px", MediaColor.FromRgb(249, 226, 175));
            }
        }
    }

    private void AddPin(double x, double y, string title, string detail, MediaColor themeColor)
    {
        var outerRing = new Ellipse
        {
            Width = 26,
            Height = 26,
            Stroke = new SolidColorBrush(themeColor),
            StrokeThickness = 2.5,
            Fill = new SolidColorBrush(MediaColor.FromArgb(90, themeColor.R, themeColor.G, themeColor.B))
        };
        Canvas.SetLeft(outerRing, x - 13);
        Canvas.SetTop(outerRing, y - 13);
        OverlayCanvas.Children.Add(outerRing);

        var centerDot = new Ellipse
        {
            Width = 8,
            Height = 8,
            Fill = new SolidColorBrush(Colors.White)
        };
        Canvas.SetLeft(centerDot, x - 4);
        Canvas.SetTop(centerDot, y - 4);
        OverlayCanvas.Children.Add(centerDot);

        double labelOffsetX = 35;
        double labelOffsetY = -28;

        if (x > Width - 180)
        {
            labelOffsetX = -180;
        }
        if (y < 60)
        {
            labelOffsetY = 25;
        }

        var line = new Line
        {
            X1 = x,
            Y1 = y,
            X2 = x + labelOffsetX + (labelOffsetX > 0 ? 0 : 160),
            Y2 = y + labelOffsetY + 12,
            Stroke = new SolidColorBrush(themeColor),
            StrokeThickness = 1.8,
            StrokeDashArray = new DoubleCollection { 3, 2 }
        };
        OverlayCanvas.Children.Add(line);

        var badge = new Border
        {
            Background = new SolidColorBrush(MediaColor.FromArgb(235, 24, 24, 37)),
            BorderBrush = new SolidColorBrush(themeColor),
            BorderThickness = new Thickness(1.5),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(8, 4, 8, 4),
            Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                Color = Colors.Black,
                BlurRadius = 8,
                Opacity = 0.8,
                ShadowDepth = 2
            }
        };

        var sp = new StackPanel();
        sp.Children.Add(new TextBlock
        {
            Text = title,
            FontWeight = FontWeights.Bold,
            FontSize = 11,
            Foreground = new SolidColorBrush(themeColor)
        });
        sp.Children.Add(new TextBlock
        {
            Text = detail,
            FontSize = 10,
            Foreground = new SolidColorBrush(MediaColor.FromRgb(205, 214, 244))
        });
        badge.Child = sp;

        Canvas.SetLeft(badge, x + labelOffsetX);
        Canvas.SetTop(badge, y + labelOffsetY);
        OverlayCanvas.Children.Add(badge);
    }

    private void Window_MouseDown(object sender, WpfMouseButtonEventArgs e)
    {
        Close();
    }

    private void Window_KeyDown(object sender, WpfKeyEventArgs e)
    {
        if (e.Key == WpfKey.Escape)
        {
            Close();
        }
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
