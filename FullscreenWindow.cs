using System.Runtime.InteropServices;
using System.Text;

namespace CodexMinder;

internal static class FullscreenWindow
{
    public static bool CoversWidgetMonitor(IntPtr widget)
    {
        var foreground = GetForegroundWindow();
        if (foreground == IntPtr.Zero || foreground == widget || !IsWindowVisible(foreground) || IsIconic(foreground)) return false;
        GetWindowThreadProcessId(foreground, out var process);
        if (process == Environment.ProcessId) return false;
        var name = new StringBuilder(256);
        GetClassName(foreground, name, name.Capacity);
        if (name.ToString() is "Progman" or "WorkerW" or "Shell_TrayWnd" or "Shell_SecondaryTrayWnd") return false;
        if (DwmGetWindowAttribute(foreground, 14, out int cloaked, sizeof(int)) == 0 && cloaked != 0) return false;

        var monitor = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (!GetMonitorInfo(MonitorFromWindow(widget, 2), ref monitor)) return false;
        if (DwmGetWindowAttribute(foreground, 9, out NativeRect bounds, Marshal.SizeOf<NativeRect>()) != 0 && !GetWindowRect(foreground, out bounds)) return false;
        bool caption = (GetWindowLong(foreground, -16) & 0x00C00000) == 0x00C00000;
        return IsFullscreen(bounds.Rectangle, monitor.Monitor.Rectangle, IsZoomed(foreground), caption);
    }

    internal static bool IsFullscreen(Rectangle window, Rectangle monitor, bool maximized = false, bool hasCaption = false)
    {
        // A conventional maximized application can cover the monitor when the
        // taskbar auto-hides; it is still a normal desktop window.
        if (maximized && hasCaption) return false;
        return window.Width > 0 && window.Height > 0 && monitor.Width > 0 && monitor.Height > 0
            && window.Left <= monitor.Left + 1 && window.Top <= monitor.Top + 1
            && window.Right >= monitor.Right - 1 && window.Bottom >= monitor.Bottom - 1;
    }

    [StructLayout(LayoutKind.Sequential)] struct NativeRect
    {
        public int Left, Top, Right, Bottom;
        public Rectangle Rectangle => Rectangle.FromLTRB(Left, Top, Right, Bottom);
    }
    [StructLayout(LayoutKind.Sequential)] struct MonitorInfo { public int Size; public NativeRect Monitor, Work; public uint Flags; }
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll")] static extern bool IsIconic(IntPtr window);
    [DllImport("user32.dll")] static extern bool IsZoomed(IntPtr window);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr window, StringBuilder name, int length);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")] static extern int GetWindowLong(IntPtr window, int index);
    [DllImport("user32.dll")] static extern IntPtr MonitorFromWindow(IntPtr window, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr window, out NativeRect bounds);
    [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr window, int attribute, out NativeRect value, int size);
    [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr window, int attribute, out int value, int size);
}
