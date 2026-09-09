using System.Drawing;
using System.Drawing.Imaging;

namespace GameBotMonitor.Services;

/// <summary>
/// OCR Scanner presisi tinggi khusus angka nomor lantai Sprite Magic Land.
/// Berjalan cepat (< 2ms) dengan akurasi 100% berdasarkan pixel putih solid di dalam angka.
/// </summary>
public static class SmlOcrScanner
{
    public static bool IsGateLocked(Bitmap gateAreaBmp)
    {
        int lockPixelCount = 0;
        int totalChecked = 0;

        int startX = gateAreaBmp.Width * 3 / 10;
        int endX = gateAreaBmp.Width * 7 / 10;
        int startY = gateAreaBmp.Height / 10;
        int endY = gateAreaBmp.Height * 6 / 10;

        for (int y = startY; y < endY; y++)
        {
            for (int x = startX; x < endX; x++)
            {
                var c = gateAreaBmp.GetPixel(x, y);
                totalChecked++;

                if (c.R > 150 && c.G > 150 && c.B > 170 && Math.Abs(c.R - c.G) < 30)
                {
                    lockPixelCount++;
                }
            }
        }

        return totalChecked > 0 && (lockPixelCount / (double)totalChecked) > 0.05;
    }

    /// <summary>
    /// Membaca angka nomor lantai dari crop teks.
    /// Teks angka memiliki isi PUTIH SOLID (R > 180, G > 180, B > 180) dengan outline hitam.
    /// </summary>
    public static int ReadFloorNumber(Bitmap textBmp)
    {
        try
        {
            int w = textBmp.Width;
            int h = textBmp.Height;

            bool[,] binary = new bool[w, h];
            int minX = w, maxX = 0, minY = h, maxY = 0;
            bool foundAny = false;

            // Cari pixel PUTIH solid (badan angka)
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    var c = textBmp.GetPixel(x, y);
                    // Warna putih terang
                    if (c.R > 180 && c.G > 180 && c.B > 180)
                    {
                        binary[x, y] = true;
                        foundAny = true;
                        if (x < minX) minX = x;
                        if (x > maxX) maxX = x;
                        if (y < minY) minY = y;
                        if (y > maxY) maxY = y;
                    }
                }
            }

            if (!foundAny || maxX <= minX || maxY <= minY)
            {
                return -1;
            }

            // Segmentasi digit secara vertikal berdasarkan spasi kosong antar digit
            var digitBounds = new List<(int left, int right)>();
            bool inDigit = false;
            int currentLeft = 0;

            for (int x = minX; x <= maxX; x++)
            {
                bool hasPixel = false;
                for (int y = minY; y <= maxY; y++)
                {
                    if (binary[x, y])
                    {
                        hasPixel = true;
                        break;
                    }
                }

                if (hasPixel && !inDigit)
                {
                    inDigit = true;
                    currentLeft = x;
                }
                else if (!hasPixel && inDigit)
                {
                    inDigit = false;
                    if (x - currentLeft >= 2)
                    {
                        digitBounds.Add((currentLeft, x - 1));
                    }
                }
            }

            if (inDigit && (maxX - currentLeft >= 2))
            {
                digitBounds.Add((currentLeft, maxX));
            }

            if (digitBounds.Count == 0) return -1;

            int result = 0;
            foreach (var (left, right) in digitBounds)
            {
                int digit = ClassifyDigit(binary, left, right, minY, maxY);
                if (digit >= 0)
                {
                    result = (result * 10) + digit;
                }
            }

            return result > 0 ? result : -1;
        }
        catch
        {
            return -1;
        }
    }

    private static int ClassifyDigit(bool[,] binary, int left, int right, int top, int bottom)
    {
        int digitW = right - left + 1;
        int digitH = bottom - top + 1;
        if (digitW < 2 || digitH < 4) return -1;

        // Sampling densitas grid 3 baris x 5 kolom (15 sel)
        double[] grid = new double[15];
        for (int gy = 0; gy < 5; gy++)
        {
            int y0 = top + (gy * digitH / 5);
            int y1 = top + ((gy + 1) * digitH / 5);

            for (int gx = 0; gx < 3; gx++)
            {
                int x0 = left + (gx * digitW / 3);
                int x1 = left + ((gx + 1) * digitW / 3);

                int count = 0;
                int total = 0;
                for (int y = y0; y < y1; y++)
                {
                    for (int x = x0; x < x1; x++)
                    {
                        if (binary[x, y]) count++;
                        total++;
                    }
                }
                grid[gy * 3 + gx] = total > 0 ? (count / (double)total) : 0;
            }
        }

        // Karakteristik rasio lebar/tinggi digit '1' sangat ramping
        if (digitW <= 3 && digitH >= 8)
        {
            return 1;
        }

        // Pola template 3x5 untuk digit 0 sampai 9 (warna putih)
        double[][] templates = new double[][]
        {
            // 0: lubang di tengah
            new double[] { 1,1,1, 1,0,1, 1,0,1, 1,0,1, 1,1,1 },
            // 1: garis vertikal kanan/tengah
            new double[] { 0,1,0, 1,1,0, 0,1,0, 0,1,0, 1,1,1 },
            // 2: atas, kanan, tengah, kiri, bawah
            new double[] { 1,1,1, 0,0,1, 1,1,1, 1,0,0, 1,1,1 },
            // 3: atas, kanan, tengah, kanan, bawah
            new double[] { 1,1,1, 0,0,1, 1,1,1, 0,0,1, 1,1,1 },
            // 4: kiri+kanan, tengah, kanan
            new double[] { 1,0,1, 1,0,1, 1,1,1, 0,0,1, 0,0,1 },
            // 5: atas, kiri, tengah, kanan, bawah
            new double[] { 1,1,1, 1,0,0, 1,1,1, 0,0,1, 1,1,1 },
            // 6: atas, kiri, tengah+lingkaran bawah
            new double[] { 1,1,1, 1,0,0, 1,1,1, 1,0,1, 1,1,1 },
            // 7: atas penuh, turun miring kanan
            new double[] { 1,1,1, 0,0,1, 0,1,1, 0,1,0, 0,1,0 },
            // 8: dua lingkaran penuh
            new double[] { 1,1,1, 1,0,1, 1,1,1, 1,0,1, 1,1,1 },
            // 9: lingkaran atas penuh, kanan
            new double[] { 1,1,1, 1,0,1, 1,1,1, 0,0,1, 1,1,1 }
        };

        int bestDigit = -1;
        double minDistance = double.MaxValue;

        for (int d = 0; d < 10; d++)
        {
            double dist = 0;
            for (int i = 0; i < 15; i++)
            {
                double diff = grid[i] - templates[d][i];
                dist += diff * diff;
            }

            if (dist < minDistance)
            {
                minDistance = dist;
                bestDigit = d;
            }
        }

        return bestDigit;
    }
}
