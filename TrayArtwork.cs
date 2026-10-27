using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace CodexMinder;

internal enum TrayStatus { Unknown, Green, Yellow, Orange, Red }

internal static class TrayArtwork
{
    public static TrayStatus StatusFor(Usage? usage)
    {
        if (usage?.FiveHour is null && usage?.Weekly is null) return TrayStatus.Unknown;
        return usage!.Level switch
        {
            UsageLevel.Yellow => TrayStatus.Yellow,
            UsageLevel.Orange => TrayStatus.Orange,
            UsageLevel.Red => TrayStatus.Red,
            _ => TrayStatus.Green
        };
    }
    public static Color BadgeColor(TrayStatus status, bool light) => status switch
    {
        TrayStatus.Green => light ? Color.FromArgb(34, 133, 78) : Color.FromArgb(102, 216, 154),
        TrayStatus.Yellow => light ? Color.FromArgb(145, 110, 12) : Color.FromArgb(220, 193, 92),
        TrayStatus.Orange => Color.FromArgb(213, 153, 87),
        TrayStatus.Red => light ? Color.FromArgb(186, 45, 45) : Color.FromArgb(230, 106, 106),
        _ => light ? Color.FromArgb(65, 125, 175) : Color.FromArgb(126, 177, 215)
    };

    public static Bitmap Render(GraphicsPath blossom, TrayStatus status, bool light, int size)
    {
        var bitmap = new Bitmap(size, size, PixelFormat.Format32bppPArgb);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.Transparent);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
        OpenAiLogo.Draw(graphics, blossom, new Rectangle(0, 0, size, size), light ? Color.FromArgb(28, 28, 30) : Color.White);
        float diameter = size * .55f;
        var badge = new RectangleF(size - diameter, size - diameter, diameter, diameter);
        // Cut out the original approved circle, letting the real tray show through.
        graphics.CompositingMode = CompositingMode.SourceCopy;
        using (var clear = new SolidBrush(Color.Transparent)) graphics.FillEllipse(clear, badge);
        graphics.CompositingMode = CompositingMode.SourceOver;
        using var font = new Font("Segoe UI", diameter * .72f, FontStyle.Bold, GraphicsUnit.Pixel);
        using var brush = new SolidBrush(BadgeColor(status, light));
        using var format = new StringFormat(StringFormat.GenericTypographic) { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        graphics.DrawString("%", font, brush, badge, format);
        return bitmap;
    }

    public static Icon Create(GraphicsPath blossom, TrayStatus status, bool light, int size)
    {
        using var bitmap = Render(blossom, status, light, size);
        var handle = bitmap.GetHicon();
        try
        {
            using var borrowed = Icon.FromHandle(handle);
            return (Icon)borrowed.Clone();
        }
        finally { DestroyIcon(handle); }
    }

    [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr icon);
}
