using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using GameBotMonitor.Models;
using GameBotMonitor.Native;

namespace GameBotMonitor.Services;

public static class SmlService
{
    public static void ClickClientPoint(nint hWnd, int clientX, int clientY)
    {
        var pt = new Win32.POINT { X = clientX, Y = clientY };
        Win32.ClientToScreen(hWnd, ref pt);

        Win32.SetCursorPos(pt.X, pt.Y);
        Thread.Sleep(60);

        int structSize = Marshal.SizeOf(typeof(Win32.INPUT));

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

    public static void ScrollMouseWheel(nint hWnd, int clientX, int clientY, int notches)
    {
        var pt = new Win32.POINT { X = clientX, Y = clientY };
        Win32.ClientToScreen(hWnd, ref pt);

        Win32.SetCursorPos(pt.X, pt.Y);
        Thread.Sleep(80);

        int structSize = Marshal.SizeOf(typeof(Win32.INPUT));
        int delta = notches * Win32.WHEEL_DELTA;

        var wheelInput = new Win32.INPUT[1];
        wheelInput[0] = new Win32.INPUT
        {
            type = Win32.INPUT_MOUSE,
            u = new Win32.InputUnion
            {
                mi = new Win32.MOUSEINPUT
                {
                    dx = 0,
                    dy = 0,
                    mouseData = (uint)delta,
                    dwFlags = Win32.MOUSEEVENTF_WHEEL,
                    time = 0,
                    dwExtraInfo = IntPtr.Zero
                }
            }
        };
        Win32.SendInput(1, wheelInput, structSize);
    }

    public static Bitmap? CaptureClientArea(nint hWnd, int clientX, int clientY, int width, int height)
    {
        try
        {
            var pt = new Win32.POINT { X = clientX, Y = clientY };
            if (!Win32.ClientToScreen(hWnd, ref pt)) return null;

            var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.CopyFromScreen(pt.X, pt.Y, 0, 0, new Size(width, height), CopyPixelOperation.SourceCopy);
            }
            return bmp;
        }
        catch
        {
            return null;
        }
    }

    public static async Task<(bool success, string? imagePath, string message)> EnterSmlAsync(
        nint hWnd,
        int slotNum,
        int smlLevel,
        string floorTarget,
        string windowTitle,
        Action<nint, ushort, int> sendHardwareKeyFunc,
        Func<bool> isHpAliveFunc,
        SmlConfig? config = null,
        Action<string>? logFunc = null)
    {
        config ??= new SmlConfig();

        if (hWnd == 0)
        {
            return (false, null, $"❌ Slot {slotNum} tidak aktif atau jendela tidak ditemukan.");
        }

        smlLevel = Math.Clamp(smlLevel, 1, 4);

        try
        {
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

            Win32.SetForegroundWindow(hWnd);
            await Task.Delay(1000);

            // Buka menu Dungeon Management
            sendHardwareKeyFunc(hWnd, config.DungeonScanCode, 150);
            await Task.Delay(1500);

            // Klik Tab Sprite Magic Land
            int tabX = (int)(w * (config.TabSmlPercentX / 100.0));
            int tabY = (int)(h * (config.TabSmlPercentY / 100.0));
            ClickClientPoint(hWnd, tabX, tabY);
            await Task.Delay(1000);

            // Pilih Level SML
            int levelX;
            int levelY;
            switch (smlLevel)
            {
                case 1:
                    levelX = (int)(w * (config.Level1PercentX / 100.0));
                    levelY = (int)(h * (config.Level1PercentY / 100.0));
                    break;
                case 2:
                    levelX = (int)(w * (config.Level2PercentX / 100.0));
                    levelY = (int)(h * (config.Level2PercentY / 100.0));
                    break;
                case 3:
                    levelX = (int)(w * (config.Level3PercentX / 100.0));
                    levelY = (int)(h * (config.Level3PercentY / 100.0));
                    break;
                default:
                    levelX = (int)(w * (config.Level4PercentX / 100.0));
                    levelY = (int)(h * (config.Level4PercentY / 100.0));
                    break;
            }
            ClickClientPoint(hWnd, levelX, levelY);
            await Task.Delay(1000);

            int towerX = (int)(w * (config.TowerCenterPercentX / 100.0));
            int towerY = (int)(h * (config.TowerCenterPercentY / 100.0));
            int gateX = (int)(w * (config.GatePercentX / 100.0));
            int gateY = (int)(h * (config.GatePercentY / 100.0));

            string cleanFloor = floorTarget.Trim().ToLowerInvariant();

            // Folder Debug untuk menyimpan potongan scan angka
            string debugDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Capture", "Sml", "Debug");
            Directory.CreateDirectory(debugDir);

            // ==================== SCANNING FLOOR (OCR & PADLOCK DETECTION) ====================

            // 1. Scan nomor lantai saat ini (crop di atas gerbang aktif)
            int detectedFloor = -1;
            using (var numberBmp = CaptureClientArea(hWnd, gateX - 25, gateY - 32, 50, 26))
            {
                if (numberBmp != null)
                {
                    string debugPath = Path.Combine(debugDir, $"scan_initial_slot{slotNum}.png");
                    numberBmp.Save(debugPath, ImageFormat.Png);
                    detectedFloor = SmlOcrScanner.ReadFloorNumber(numberBmp);
                    logFunc?.Invoke($"[OCR-SCAN] Slot {slotNum}: Posisi awal angka terbaca = {(detectedFloor > 0 ? detectedFloor.ToString() : "N/A")} | Crop: {Path.GetFileName(debugPath)}");
                }
            }

            // 2. Scan apakah gerbang saat ini terkunci gembok
            bool isCurrentLocked = false;
            using (var lockBmp = CaptureClientArea(hWnd, gateX - 35, gateY - 10, 70, 45))
            {
                if (lockBmp != null)
                {
                    isCurrentLocked = SmlOcrScanner.IsGateLocked(lockBmp);
                    logFunc?.Invoke($"[OCR-SCAN] Slot {slotNum}: Status gerbang saat ini = {(isCurrentLocked ? "TERKUNCI (Padlock)" : "TERBUKA (Bisa Masuk)")}");
                }
            }

            // 3. Logika penyesuaian navigasi Floor
            if (cleanFloor == "max")
            {
                // Jika lantai yang disorot saat ini terkunci (Padlock), scroll down 1 per 1 sampai ketemu lantai terbuka
                int retries = 5;
                while (isCurrentLocked && retries > 0)
                {
                    logFunc?.Invoke($"[OCR-NAV] Lantai masih terkunci. Scroll down 1 langkah mencari lantai terbuka...");
                    ScrollMouseWheel(hWnd, towerX, towerY, -1);
                    await Task.Delay(400);

                    using var checkBmp = CaptureClientArea(hWnd, gateX - 35, gateY - 10, 70, 45);
                    isCurrentLocked = checkBmp != null && SmlOcrScanner.IsGateLocked(checkBmp);
                    retries--;
                }
            }
            else if (cleanFloor.StartsWith('-') && int.TryParse(cleanFloor.Substring(1), out int downSteps))
            {
                logFunc?.Invoke($"[OCR-NAV] Menjalankan scroll down {downSteps} langkah dari lantai saat ini...");
                for (int s = 0; s < downSteps; s++)
                {
                    ScrollMouseWheel(hWnd, towerX, towerY, -1);
                    await Task.Delay(300);
                }
                await Task.Delay(500);
            }
            else if (int.TryParse(cleanFloor, out int targetFloorNum))
            {
                if (detectedFloor > 0 && targetFloorNum < detectedFloor)
                {
                    int stepsNeeded = detectedFloor - targetFloorNum;
                    logFunc?.Invoke($"[OCR-NAV] Target {targetFloorNum} < Terbaca {detectedFloor}. Scroll down {stepsNeeded} langkah...");
                    for (int s = 0; s < stepsNeeded; s++)
                    {
                        ScrollMouseWheel(hWnd, towerX, towerY, -1);
                        await Task.Delay(300);
                    }
                    await Task.Delay(500);
                }
            }

            // Scan ulang hasil akhir floor setelah navigasi
            int finalFloor = -1;
            using (var finalNumberBmp = CaptureClientArea(hWnd, gateX - 25, gateY - 32, 50, 26))
            {
                if (finalNumberBmp != null)
                {
                    string debugFinalPath = Path.Combine(debugDir, $"scan_final_slot{slotNum}.png");
                    finalNumberBmp.Save(debugFinalPath, ImageFormat.Png);
                    finalFloor = SmlOcrScanner.ReadFloorNumber(finalNumberBmp);
                    logFunc?.Invoke($"[OCR-SCAN] Slot {slotNum}: Lantai final siap masuk = {(finalFloor > 0 ? finalFloor.ToString() : (detectedFloor > 0 ? detectedFloor.ToString() : "OK"))}");
                }
            }

            // Klik Gerbang Lantai yang sudah terkalibrasi
            ClickClientPoint(hWnd, gateX, gateY);
            await Task.Delay(1000);

            // Klik Tombol 'Enter the...' di kanan bawah
            int enterX = (int)(w * (config.EnterButtonPercentX / 100.0));
            int enterY = (int)(h * (config.EnterButtonPercentY / 100.0));
            ClickClientPoint(hWnd, enterX, enterY);
            await Task.Delay(2000);

            // Pantau loading screen & bar darah
            bool enteredDungeon = false;
            DateTime startTime = DateTime.Now;
            while ((DateTime.Now - startTime).TotalSeconds < 25)
            {
                if (isHpAliveFunc())
                {
                    enteredDungeon = true;
                    break;
                }
                await Task.Delay(1000);
            }

            string captureDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Capture", "Sml");
            Directory.CreateDirectory(captureDir);
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string imagePath = Path.Combine(captureDir, $"sml_slot{slotNum}_lv{smlLevel}_{cleanFloor}_{timestamp}.png");

            CaptureWindowToFile(hWnd, imagePath);

            int displayFloor = finalFloor > 0 ? finalFloor : (detectedFloor > 0 ? detectedFloor : -1);
            string readFloorStr = displayFloor > 0 ? $"Floor {displayFloor}" : "Terbuka Otomatis";
            string floorDesc = cleanFloor == "max" ? $"Tertinggi (Max)" : (cleanFloor.StartsWith('-') ? $"Turun {cleanFloor.Substring(1)} Lantai dari Max" : $"Floor {cleanFloor}");

            if (enteredDungeon)
            {
                string msg =
                    $"🏰 *Berhasil Masuk Sprite Magic Land!*\n" +
                    $"🎮 *Slot:* {slotNum} (`{windowTitle}`)\n" +
                    $"⭐ *Tingkat SML:* Level {smlLevel}\n" +
                    $"🚪 *Pilihan:* {floorDesc}\n" +
                    $"📊 *Floor Terbaca:* `{readFloorStr}`\n" +
                    $"🕒 *Waktu:* `{DateTime.Now:HH:mm:ss}`\n" +
                    $"❤️ *Status:* In-Dungeon (Bar HP Terdeteksi)\n" +
                    $"🎯 *Auto TAB:* Telah dipulihkan otomatis.";

                return (true, imagePath, msg);
            }
            else
            {
                string msg =
                    $"⚠️ *Proses Masuk SML Selesai (Menunggu Deteksi HP)*\n" +
                    $"🎮 *Slot:* {slotNum} (`{windowTitle}`)\n" +
                    $"⭐ *Tingkat SML:* Level {smlLevel}\n" +
                    $"🚪 *Pilihan:* {floorDesc}\n" +
                    $"📊 *Floor Terbaca:* `{readFloorStr}`\n" +
                    $"ℹ️ Tombol Enter telah diklik. Silakan periksa screenshot terlampir.";

                return (true, imagePath, msg);
            }
        }
        catch (Exception ex)
        {
            return (false, null, $"❌ Terjadi kesalahan saat masuk SML Slot {slotNum}: {ex.Message}");
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
