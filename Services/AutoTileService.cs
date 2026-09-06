using System.Text;
using GameBotMonitor.Native;

namespace GameBotMonitor.Services;

public static class AutoTileService
{
    public static int TileGameWindows(string targetTitleKeyword = "Grand Fantasia")
    {
        var handles = new List<nint>();

        Win32.EnumWindows((hWnd, lParam) =>
        {
            if (!Win32.IsWindowVisible(hWnd)) return true;

            int length = Win32.GetWindowTextLength(hWnd);
            if (length == 0) return true;

            var sb = new StringBuilder(length + 1);
            Win32.GetWindowText(hWnd, sb, sb.Capacity);
            var title = sb.ToString();

            if (title.Contains(targetTitleKeyword, StringComparison.OrdinalIgnoreCase))
            {
                handles.Add(hWnd);
            }

            return true;
        }, 0);

        if (handles.Count == 0) return 0;

        // Ambil area kerja layar utama (tidak tertutup taskbar)
        var workArea = System.Windows.SystemParameters.WorkArea;
        int screenWidth = (int)workArea.Width;
        int count = handles.Count;

        // Dapatkan ukuran jendela pertama sebagai referensi ukuran saat ini
        Win32.GetWindowRect(handles[0], out var firstRect);
        int winWidth = Math.Max(400, firstRect.Width);

        // Tentukan posisi (X, Y) TANPA mengubah ukuran jendela (SWP_NOSIZE)
        // Hal ini menjaga agar font dan resolusi in-game tidak pecah/berantakan
        if (count == 1)
        {
            var hWnd = handles[0];
            RestoreIfMinimized(hWnd);
            Win32.SetWindowPos(hWnd, 0, (int)workArea.Left, (int)workArea.Top, 0, 0, 
                Win32.SWP_NOSIZE | Win32.SWP_NOZORDER | Win32.SWP_SHOWWINDOW);
        }
        else
        {
            // Cek apakah muat berjajar tanpa bertumpuk
            bool fitsSideBySide = (count * winWidth) <= screenWidth;

            int stepX;
            if (fitsSideBySide)
            {
                stepX = winWidth;
            }
            else
            {
                // Jika melebihi lebar layar, sebar bertingkat (cascading horizontal)
                // Jarak antar jendela diatur agar sudut kiri atas (bar HP) tetap terlihat jelas
                stepX = Math.Max(200, (screenWidth - winWidth) / (count - 1));
            }

            for (int i = 0; i < count; i++)
            {
                var hWnd = handles[i];
                RestoreIfMinimized(hWnd);
                int x = (int)workArea.Left + (i * stepX);
                int y = (int)workArea.Top;

                // SWP_NOSIZE memastikan lebar dan tinggi jendela TIDAK DIUBAH sama sekali
                Win32.SetWindowPos(hWnd, 0, x, y, 0, 0, 
                    Win32.SWP_NOSIZE | Win32.SWP_NOZORDER | Win32.SWP_SHOWWINDOW);
            }
        }

        return count;
    }

    private static void RestoreIfMinimized(nint hWnd)
    {
        if (Win32.IsIconic(hWnd))
        {
            Win32.ShowWindow(hWnd, Win32.SW_RESTORE);
        }
    }
}
