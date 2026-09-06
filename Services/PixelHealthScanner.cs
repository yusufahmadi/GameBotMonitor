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

    /// <summary>
    /// Mengambil warna pixel pada koordinat relatif target frame monster
    /// </summary>
    public static (Color color, string hex) SampleTargetColor(nint hWnd, int offsetX, int offsetY)
    {
        return SampleHpColor(hWnd, offsetX, offsetY);
    }

    /// <summary>
    /// Mengevaluasi apakah target monster sedang ter-lock (ada HUD target)
    /// Jika referenceHex disediakan, periksa kecocokan warna dengan toleransi.
    /// Jika referenceHex kosong, gunakan heuristik: HUD target monster Grand Fantasia
    /// memiliki border krem/emas (R>140, G>130, B>100) atau bar HP merah muda (R>140, G<120)
    /// </summary>
    public static bool IsTargetMonsterPresent(Color color, string? referenceHex = null)
    {
        if (!string.IsNullOrWhiteSpace(referenceHex) && referenceHex.StartsWith('#') && referenceHex.Length == 7)
        {
            try
            {
                int r = Convert.ToInt32(referenceHex.Substring(1, 2), 16);
                int g = Convert.ToInt32(referenceHex.Substring(3, 2), 16);
                int b = Convert.ToInt32(referenceHex.Substring(5, 2), 16);

                // Toleransi perbedaan warna (RGB distance <= 50)
                int diff = Math.Abs(color.R - r) + Math.Abs(color.G - g) + Math.Abs(color.B - b);
                return diff <= 55;
            }
            catch
            {
                // Fallback jika hex salah format
            }
        }

        // Heuristik Grand Fantasia:
        // Saat ada monster, bar HP monster berwarna merah/pink (R tinggi, R - G >= 20)
        // ATAU bingkai ornamen frame monster berwarna krem/gading (R>150, G>140, B>120)
        // Saat TIDAK ada monster, area tersebut adalah background alam game yang gelap/hijau rumput/langit
        bool isHpBar = color.R > 130 && (color.R - color.G >= 20);
        bool isFrameBorder = (color.R > 150 && color.G > 140 && color.B > 115) && (Math.Abs(color.R - color.G) < 30);

        return isHpBar || isFrameBorder;
    }
}
