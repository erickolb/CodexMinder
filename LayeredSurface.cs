using System.ComponentModel;
using System.Runtime.InteropServices;

namespace CodexMinder;

internal static class LayeredSurface
{
    // ULW_ALPHA uses the bitmap's premultiplied alpha, preserving smooth glyph edges.
    public static void Present(IntPtr window, Bitmap bitmap, Point location)
    {
        var dc = CreateCompatibleDC(IntPtr.Zero);
        if (dc == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
        IntPtr image = IntPtr.Zero, previous = IntPtr.Zero;
        try
        {
            image = bitmap.GetHbitmap(Color.FromArgb(0));
            previous = SelectObject(dc, image);
            var origin = Point.Empty;
            var size = bitmap.Size;
            var blend = new Blend { SourceConstantAlpha = 255, AlphaFormat = 1 };
            if (!UpdateLayeredWindow(window, IntPtr.Zero, ref location, ref size, dc, ref origin, 0, ref blend, 2))
                throw new Win32Exception(Marshal.GetLastWin32Error());
        }
        finally
        {
            if (previous != IntPtr.Zero) SelectObject(dc, previous);
            if (image != IntPtr.Zero) DeleteObject(image);
            DeleteDC(dc);
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    struct Blend { public byte BlendOp, BlendFlags, SourceConstantAlpha, AlphaFormat; }
    [DllImport("user32.dll", SetLastError = true)] static extern bool UpdateLayeredWindow(IntPtr window, IntPtr destinationDc, ref Point destination, ref Size size, IntPtr sourceDc, ref Point source, uint colorKey, ref Blend blend, uint flags);
    [DllImport("gdi32.dll", SetLastError = true)] static extern IntPtr CreateCompatibleDC(IntPtr dc);
    [DllImport("gdi32.dll")] static extern IntPtr SelectObject(IntPtr dc, IntPtr value);
    [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr value);
    [DllImport("gdi32.dll")] static extern bool DeleteDC(IntPtr dc);
}
