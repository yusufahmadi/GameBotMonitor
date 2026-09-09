using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using GameBotMonitor.Models;
using GameBotMonitor.Native;

namespace GameBotMonitor.Services;

public static class CharacterSwitcherService
{
    public static Color SampleClientPixel(nint hWnd, int clientX, int clientY)
    {
        nint hdc = Win32.GetDC(hWnd);
        if (hdc == IntPtr.Zero) return Color.Black;
        try
        {
            uint pixel = Win32.GetPixel(hdc, clientX, clientY);
            if (pixel == 0xFFFFFFFF) return Color.Black;
            byte r = (byte)(pixel & 0x000000FF);
            byte g = (byte)((pixel & 0x0000FF00) >> 8);
            byte b = (byte)((pixel & 0x00FF0000) >> 16);
            return Color.FromArgb(r, g, b);
        }
        catch
        {
            return Color.Black;
        }
        finally
        {
            Win32.ReleaseDC(hWnd, hdc);
        }
    }

    private static bool IsDialogCreamColor(Color c)
    {
        // Dialog System & Quit memiliki frame beige/cream (R > 180, G > 170, B > 140)
        return c.R > 180 && c.G > 170 && c.B > 140 && c.R >= c.B;
    }

    private static bool IsSystemMenuOpen(nint hWnd, int cx, int cy, int logoutOffsetY)
    {
        var cCenter = SampleClientPixel(hWnd, cx, cy);
        var cLogout = SampleClientPixel(hWnd, cx, cy + logoutOffsetY);
        return IsDialogCreamColor(cCenter) || IsDialogCreamColor(cLogout);
    }

    public static void ClickClientPoint(nint hWnd, int clientX, int clientY)
    {
        var pt = new Win32.POINT { X = clientX, Y = clientY };
        Win32.ClientToScreen(hWnd, ref pt);

        Win32.SetCursorPos(pt.X, pt.Y);
        Thread.Sleep(60);

        int structSize = Marshal.SizeOf(typeof(Win32.INPUT));

        // Mouse Down
        var inputDown = new Win32.INPUT[1];
        inputDown[0] = new Win32.INPUT
        {
            type = Win32.INPUT_MOUSE,
            u = new Win32.InputUnion
            {
                mi = new Win32.MOUSEINPUT
                {
                    dx = 0,
                    dy = 0,
                    mouseData = 0,
                    dwFlags = Win32.MOUSEEVENTF_LEFTDOWN,
                    time = 0,
                    dwExtraInfo = IntPtr.Zero
                }
            }
        };
        Win32.SendInput(1, inputDown, structSize);

        Thread.Sleep(90);

        // Mouse Up
        var inputUp = new Win32.INPUT[1];
        inputUp[0] = new Win32.INPUT
        {
            type = Win32.INPUT_MOUSE,
            u = new Win32.InputUnion
            {
                mi = new Win32.MOUSEINPUT
                {
                    dx = 0,
                    dy = 0,
                    mouseData = 0,
                    dwFlags = Win32.MOUSEEVENTF_LEFTUP,
                    time = 0,
                    dwExtraInfo = IntPtr.Zero
                }
            }
        };
        Win32.SendInput(1, inputUp, structSize);
    }

    public static async Task<(bool success, string? imagePath, string message)> SwitchCharacterAsync(
        nint hWnd,
        int slotNum,
        int targetCharNum,
        string windowTitle,
        Action<nint, ushort, int> sendHardwareKeyFunc,
        Func<bool> isHpAliveFunc,
        CharacterSwitchConfig? config = null)
    {
        config ??= new CharacterSwitchConfig();

        if (hWnd == 0)
        {
            return (false, null, $"❌ Slot {slotNum} tidak aktif atau jendela tidak ditemukan.");
        }

        if (targetCharNum < 1 || targetCharNum > 9)
        {
            return (false, null, $"⚠️ Nomor karakter harus antara 1 sampai 9 (diminta: {targetCharNum}).");
        }

        try
        {
            // 1. Dapatkan dimensi jendela client
            if (!Win32.GetClientRect(hWnd, out var clientRect) || clientRect.Width <= 0 || clientRect.Height <= 0)
            {
                if (!Win32.GetWindowRect(hWnd, out var winRect))
                {
                    return (false, null, $"❌ Gagal mendeteksi koordinat jendela Slot {slotNum}.");
                }
                clientRect.Right = winRect.Width;
                clientRect.Bottom = winRect.Height;
            }

            int w = clientRect.Width;
            int h = clientRect.Height;
            int cx = w / 2;
            int cy = h / 2;

            // 2. Bawa jendela ke Foreground
            Win32.SetForegroundWindow(hWnd);
            await Task.Delay(1000); // Jeda 1 detik setelah fokus jendela

            // Koordinat tombol Return dialog countdown
            int returnX = cx + config.ReturnOffsetX;
            int returnY = cy + config.ReturnOffsetY;

            // Cek apakah dialog Quit (Return / Cancel) sudah terbuka di layar
            var cReturnArea = SampleClientPixel(hWnd, returnX, returnY);
            bool isQuitAlreadyOpen = IsDialogCreamColor(cReturnArea);

            if (!isQuitAlreadyOpen)
            {
                // Cek apakah menu System sudah dalam keadaan terbuka sebelum menekan ESC
                bool isMenuOpen = IsSystemMenuOpen(hWnd, cx, cy, config.LogoutOffsetY);

                if (!isMenuOpen)
                {
                    // Belum terbuka, tekan ESC untuk memunculkan menu System
                    sendHardwareKeyFunc(hWnd, Win32.SCANCODE_ESC, 150);
                    await Task.Delay(1000); // Jeda 1 detik setelah tekan ESC

                    // Verifikasi: jika setelah tekan ESC ternyata menu belum terbuka
                    // (misal karena sebelumnya menu sudah terbuka dan ESC tadi menutupnya)
                    if (!IsSystemMenuOpen(hWnd, cx, cy, config.LogoutOffsetY))
                    {
                        // Tekan ESC sekali lagi untuk memunculkannya kembali
                        sendHardwareKeyFunc(hWnd, Win32.SCANCODE_ESC, 150);
                        await Task.Delay(1000);
                    }
                }

                // 3. Klik tombol 'Logout' (offset dari config: CX + LogoutOffsetX, CY + LogoutOffsetY)
                int logoutX = cx + config.LogoutOffsetX;
                int logoutY = cy + config.LogoutOffsetY;
                ClickClientPoint(hWnd, logoutX, logoutY);
                await Task.Delay(1000); // Jeda 1 detik setelah klik Logout agar dialog countdown muncul
            }

            // 4. Klik tombol 'Return' pada pop-up countdown untuk bypass 10 detik
            ClickClientPoint(hWnd, returnX, returnY);
            await Task.Delay(1000); // Jeda 1 detik setelah klik Return

            // 5. Tunggu proses transisi ke Character Selection Screen (3.5 detik)
            await Task.Delay(3500);

            // 6. Hitung Halaman & Slot Kartu
            int targetPage = ((targetCharNum - 1) / 3) + 1; // 1, 2, atau 3
            int cardIndex = ((targetCharNum - 1) % 3) + 1;  // 1, 2, atau 3

            // Jika butuh halaman 2 atau 3, klik panah ▶
            if (targetPage > 1)
            {
                int nextArrowX = (int)(w * (config.PageNextPercentX / 100.0));
                int nextArrowY = (int)(h * (config.PageNextPercentY / 100.0));

                for (int p = 1; p < targetPage; p++)
                {
                    ClickClientPoint(hWnd, nextArrowX, nextArrowY);
                    await Task.Delay(1000); // Jeda 1 detik antar perpindahan halaman
                }
            }

            // 7. Klik Kartu Karakter di HUD sebelah kanan
            int cardX;
            int cardY;

            switch (cardIndex)
            {
                case 1:
                    cardX = (int)(w * (config.Card1PercentX / 100.0));
                    cardY = (int)(h * (config.Card1PercentY / 100.0));
                    break;
                case 2:
                    cardX = (int)(w * (config.Card2PercentX / 100.0));
                    cardY = (int)(h * (config.Card2PercentY / 100.0));
                    break;
                default:
                    cardX = (int)(w * (config.Card3PercentX / 100.0));
                    cardY = (int)(h * (config.Card3PercentY / 100.0));
                    break;
            }

            ClickClientPoint(hWnd, cardX, cardY);
            await Task.Delay(1000); // Jeda 1 detik setelah memilih kartu karakter

            // 8. Klik tombol 'Start Game' di bagian bawah
            int startX = (int)(w * (config.StartGamePercentX / 100.0));
            int startY = (int)(h * (config.StartGamePercentY / 100.0));
            ClickClientPoint(hWnd, startX, startY);
            await Task.Delay(1500); // Jeda 1.5 detik setelah klik Start Game

            // 9. Tunggu loading screen & polling deteksi bar HP (timeout 25 detik)
            await Task.Delay(3000);

            bool enteredWorld = false;
            DateTime startTime = DateTime.Now;
            while ((DateTime.Now - startTime).TotalSeconds < 25)
            {
                if (isHpAliveFunc())
                {
                    enteredWorld = true;
                    break;
                }
                await Task.Delay(1000);
            }

            // 10. Ambil screenshot konfirmasi
            string captureDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Capture", "CharacterSwitch");
            Directory.CreateDirectory(captureDir);
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string imagePath = Path.Combine(captureDir, $"switch_slot{slotNum}_char{targetCharNum}_{timestamp}.png");

            CaptureWindowToFile(hWnd, imagePath);

            if (enteredWorld)
            {
                string msg = 
                    $"✅ *Berhasil Berganti Karakter!*\n" +
                    $"🎮 *Slot:* {slotNum} (`{windowTitle}`)\n" +
                    $"👤 *Karakter Target:* Nomor {targetCharNum} (Hal. {targetPage}, Kartu {cardIndex})\n" +
                    $"🕒 *Waktu:* `{DateTime.Now:HH:mm:ss}`\n" +
                    $"❤️ *Status:* In-Game Online (HP Bar Terdeteksi)\n" +
                    $"🎯 *Auto TAB:* Telah dipulihkan otomatis.";

                return (true, imagePath, msg);
            }
            else
            {
                string msg = 
                    $"⚠️ *Proses Ganti Karakter Selesai (Menunggu Verifikasi HP)*\n" +
                    $"🎮 *Slot:* {slotNum} (`{windowTitle}`)\n" +
                    $"👤 *Karakter Target:* Nomor {targetCharNum}\n" +
                    $"ℹ️ Tombol Start Game telah diklik. Silakan periksa screenshot terlampir untuk memastikan status in-game.";

                return (true, imagePath, msg);
            }
        }
        catch (Exception ex)
        {
            return (false, null, $"❌ Terjadi kesalahan saat ganti karakter Slot {slotNum}: {ex.Message}");
        }
    }

    private static bool CaptureWindowToFile(nint hWnd, string targetPath)
    {
        try
        {
            if (!Win32.GetWindowRect(hWnd, out var rect)) return false;

            int width = rect.Width;
            int height = rect.Height;
            if (width <= 0 || height <= 0) return false;

            using var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                var hdcDest = g.GetHdc();
                bool printOk = Win32.PrintWindow(hWnd, hdcDest, Win32.PW_RENDERFULLCONTENT);
                g.ReleaseHdc(hdcDest);

                if (!printOk)
                {
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
