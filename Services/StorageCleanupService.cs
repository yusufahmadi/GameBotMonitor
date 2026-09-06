using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;

namespace GameBotMonitor.Services;

public class StorageCleanupService
{
    private readonly string _baseDirectory;
    private readonly int _maxDaysRetention;
    private readonly int _maxScreenshotsPerDay;

    public StorageCleanupService(string baseFolder = "Capture", int maxDaysRetention = 3, int maxScreenshotsPerDay = 10)
    {
        _baseDirectory = Path.IsPathRooted(baseFolder)
            ? baseFolder
            : Path.Combine(AppContext.BaseDirectory, baseFolder);
        _maxDaysRetention = Math.Max(1, maxDaysRetention);
        _maxScreenshotsPerDay = Math.Max(1, maxScreenshotsPerDay);
    }

    /// <summary>
    /// Menyimpan screenshot ke Capture/yyyy/MM/dd/ lalu melakukan auto-cleanup
    /// </summary>
    public string SaveScreenshot(Bitmap bitmap, int slotIndex)
    {
        var now = DateTime.Now;
        var dayFolder = Path.Combine(
            _baseDirectory,
            now.ToString("yyyy"),
            now.ToString("MM"),
            now.ToString("dd")
        );

        Directory.CreateDirectory(dayFolder);

        var filename = $"Capture_{now:yyyyMMdd_HHmmss}_Slot{slotIndex}.png";
        var fullPath = Path.Combine(dayFolder, filename);

        bitmap.Save(fullPath, ImageFormat.Png);

        // Jalankan pembersihan setelah menyimpan
        RunCleanup();

        return fullPath;
    }

    /// <summary>
    /// Membersihkan folder lama (> 3 hari) dan membatasi file per hari (maks 10 file terakhir)
    /// </summary>
    public void RunCleanup()
    {
        try
        {
            if (!Directory.Exists(_baseDirectory)) return;

            var today = DateTime.Today;
            var cutoffDate = today.AddDays(-(_maxDaysRetention - 1)); // Menyisakan 3 hari terakhir (hari ini, kemarin, lusa lalu)

            // 1. Telusuri folder yyyy/MM/dd
            var yearDirs = Directory.GetDirectories(_baseDirectory);
            foreach (var yearDir in yearDirs)
            {
                var yearName = Path.GetFileName(yearDir);
                if (!int.TryParse(yearName, out _)) continue;

                var monthDirs = Directory.GetDirectories(yearDir);
                foreach (var monthDir in monthDirs)
                {
                    var monthName = Path.GetFileName(monthDir);
                    if (!int.TryParse(monthName, out _)) continue;

                    var dayDirs = Directory.GetDirectories(monthDir);
                    foreach (var dayDir in dayDirs)
                    {
                        var dayName = Path.GetFileName(dayDir);
                        var dateStr = $"{yearName}-{monthName}-{dayName}";

                        if (DateTime.TryParseExact(dateStr, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var folderDate))
                        {
                            if (folderDate.Date < cutoffDate.Date)
                            {
                                // Hapus folder yang lebih tua dari 3 hari
                                try
                                {
                                    Directory.Delete(dayDir, true);
                                }
                                catch { }
                            }
                            else
                            {
                                // Folder dalam rentang 3 hari terakhir: batasi maksimal 10 file per hari
                                TrimDailyFiles(dayDir, _maxScreenshotsPerDay);
                            }
                        }
                    }

                    // Hapus folder bulan jika sudah kosong
                    if (Directory.Exists(monthDir) && Directory.GetFileSystemEntries(monthDir).Length == 0)
                    {
                        try { Directory.Delete(monthDir); } catch { }
                    }
                }

                // Hapus folder tahun jika sudah kosong
                if (Directory.Exists(yearDir) && Directory.GetFileSystemEntries(yearDir).Length == 0)
                {
                    try { Directory.Delete(yearDir); } catch { }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[StorageCleanupService] Error during cleanup: {ex.Message}");
        }
    }

    private static void TrimDailyFiles(string dayDir, int maxFiles)
    {
        try
        {
            var files = new DirectoryInfo(dayDir).GetFiles("*.png")
                .OrderBy(f => f.CreationTime)
                .ToList();

            if (files.Count > maxFiles)
            {
                var filesToDeleteCount = files.Count - maxFiles;
                for (int i = 0; i < filesToDeleteCount; i++)
                {
                    try
                    {
                        files[i].Delete();
                    }
                    catch { }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[StorageCleanupService] Trim files error: {ex.Message}");
        }
    }
}
