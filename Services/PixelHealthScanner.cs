using System.Drawing;
using GameBotMonitor.Models;
using GameBotMonitor.Native;

namespace GameBotMonitor.Services;

public static class PixelHealthScanner
{
    /// <summary>
    /// Mengambil warna pixel pada koordinat relatif bar HP jendela game
    /// </summary>
    public static (Color color, string hex) SampleHpColor(nint hWnd, int offsetX, int offsetY)
    {
        try
        {
            if (!Win32.GetWindowRect(hWnd, out var rect))
            {
                return (Color.Black, "#000000");
            }

            int targetX = rect.Left + offsetX;
            int targetY = rect.Top + offsetY;

            using var bmp = new Bitmap(1, 1);
            using (var g = Graphics.FromImage(bmp))
            {
                g.CopyFromScreen(targetX, targetY, 0, 0, new Size(1, 1));
            }

            var pixel = bmp.GetPixel(0, 0);
            var hex = $"#{pixel.R:X2}{pixel.G:X2}{pixel.B:X2}";
            return (pixel, hex);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PixelHealthScanner] Sample error: {ex.Message}");
            return (Color.Black, "#000000");
        }
    }

    /// <summary>
    /// Evaluasi apakah warna mengindikasikan HP masih ada (Hidup) atau kosong/0 (Mati)
    /// </summary>
    public static bool IsHpAlive(Color color)
    {
        // Karakter Grand Fantasia bar darah: warna merah muda / salmon pink
        // Nilai Red dominan (R > 130) dan lebih tinggi dari Green setidaknya selisih 25
        if (color.R > 130 && (color.R - color.G >= 25))
        {
            return true;
        }

        // Jika darah kosong (0 HP), bar menjadi gelap/abu-abu (R < 100)
        return false;
    }

    /// <summary>
    /// Menangkap gambar jendela game. Secara default hanya menangkap potongan HUD status karakter (avatar & bar HP)
    /// </summary>
    public static Bitmap? CaptureWindow(nint hWnd, bool cropHudOnly = true, int cropWidth = 300, int cropHeight = 140)
    {
        try
        {
            if (!Win32.GetWindowRect(hWnd, out var rect)) return null;

            int width = cropHudOnly ? Math.Min(cropWidth, rect.Width) : Math.Max(100, rect.Width);
            int height = cropHudOnly ? Math.Min(cropHeight, rect.Height) : Math.Max(100, rect.Height);

            var bmp = new Bitmap(width, height);
            using (var g = Graphics.FromImage(bmp))
            {
                g.CopyFromScreen(rect.Left, rect.Top, 0, 0, new Size(width, height));
            }

            return bmp;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PixelHealthScanner] CaptureWindow error: {ex.Message}");
            return null;
        }
    }
}
