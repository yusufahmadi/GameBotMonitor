using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace GameBotMonitor.Services;

public static class BackpackInspectorService
{
    public const ushort SCANCODE_B = 0x30; // Hardware Scan Code tombol 'B' (Backpack)

    public static async Task<(bool success, string? imagePath, string message)> InspectBackpackAsync(
        nint hWnd, 
        int slotIndex, 
        string windowTitle, 
        Action<nint, ushort, int> sendHardwareKeyFunc)
    {
        if (hWnd == 0)
        {
            return (false, null, $"❌ Slot {slotIndex} tidak aktif atau jendela tidak ditemukan.");
        }

        try
        {
            // 1. Bawa game ke Foreground
            Native.Win32.SetForegroundWindow(hWnd);
            await Task.Delay(150);

            // 2. Tekan tombol 'B' untuk membuka tas
            sendHardwareKeyFunc(hWnd, SCANCODE_B, 150);

            // 3. Beri jeda agar animasi buka tas selesai
            await Task.Delay(450);

            // 4. Ambil screenshot jendela game saat tas terbuka
            string captureDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Capture", "Backpack");
            Directory.CreateDirectory(captureDir);
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string imagePath = Path.Combine(captureDir, $"backpack_slot{slotIndex}_{timestamp}.png");

            bool captured = CaptureWindowToFile(hWnd, imagePath);

            // 5. Tekan tombol 'B' kembali untuk menutup tas agar tidak mengganggu bot farming
            sendHardwareKeyFunc(hWnd, SCANCODE_B, 150);

            if (!captured || !File.Exists(imagePath))
            {
                return (false, null, $"❌ Gagal mengambil tangkapan layar tas Slot {slotIndex}.");
            }

            string reportMsg = 
                $"🎒 *Hasil Cek Tas Slot {slotIndex}*\n" +
                $"🎮 Window: `{windowTitle}`\n" +
                $"🕒 Waktu: `{DateTime.Now:HH:mm:ss}`\n\n" +
                $"ℹ️ *Catatan:* Periksa sisa slot kosong dan total Gold pada gambar terlampir.\n" +
                $"🔒 *Status:* Jendela tas telah ditutup kembali secara otomatis.";

            return (true, imagePath, reportMsg);
        }
        catch (Exception ex)
        {
            return (false, null, $"❌ Terjadi kesalahan saat memeriksa tas: {ex.Message}");
        }
    }

    private static bool CaptureWindowToFile(nint hWnd, string targetPath)
    {
        try
        {
            if (!Native.Win32.GetWindowRect(hWnd, out var rect)) return false;

            int width = rect.Width;
            int height = rect.Height;
            if (width <= 0 || height <= 0) return false;

            using var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                var hdcDest = g.GetHdc();
                // Gunakan PrintWindow dengan fallback BitBlt
                bool printOk = Native.Win32.PrintWindow(hWnd, hdcDest, Native.Win32.PW_RENDERFULLCONTENT);
                g.ReleaseHdc(hdcDest);

                if (!printOk)
                {
                    // Fallback copy dari screen langsung jika PrintWindow kosong
                    g.CopyFromScreen(rect.Left, rect.Top, 0, 0, new Size(width, height), CopyPixelOperation.SourceCopy);
                }
            }

            bmp.Save(targetPath, ImageFormat.Png);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
