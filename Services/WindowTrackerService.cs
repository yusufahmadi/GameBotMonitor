using System.Diagnostics;
using System.Text;
using GameBotMonitor.Native;

namespace GameBotMonitor.Services;

public record GameWindowEntry(nint Handle, int ProcessId, string Title, Win32.RECT Rect, bool IsResponding);

public static class WindowTrackerService
{
    public static List<GameWindowEntry> FindGameWindows(string targetTitleKeyword = "Grand Fantasia")
    {
        var entries = new List<GameWindowEntry>();

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
                Win32.GetWindowThreadProcessId(hWnd, out var pid);
                Win32.GetWindowRect(hWnd, out var rect);

                bool isResponding = true;
                try
                {
                    var proc = Process.GetProcessById((int)pid);
                    isResponding = proc.Responding;
                }
                catch { }

                entries.Add(new GameWindowEntry(hWnd, (int)pid, title, rect, isResponding));
            }

            return true;
        }, 0);

        // Urutkan dari kiri ke kanan berdasarkan posisi X di monitor
        // Slot 1 = Jendela paling kiri, Slot 2 = Tengah, Slot 3 = Kanan
        return entries.OrderBy(e => e.Rect.Left).ToList();
    }
}
