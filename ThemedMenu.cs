using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace CodexMinder;

internal sealed class ThemedMenu : ContextMenuStrip
{
    readonly Font menuFont = new("Segoe UI", 9.5f);

    public ThemedMenu()
    {
        Font = menuFont;
        ShowImageMargin = false;
        ShowCheckMargin = true;
        Padding = new Padding(5);
    }

    protected override void OnOpening(System.ComponentModel.CancelEventArgs e)
    {
        // Read at opening so theme changes apply without restarting the app.
        bool light = SystemTheme.IsLight;
        BackColor = SystemInformation.HighContrast ? SystemColors.Menu : light ? Color.FromArgb(249, 249, 249) : Color.FromArgb(43, 43, 43);
        ForeColor = SystemInformation.HighContrast ? SystemColors.MenuText : light ? Color.FromArgb(25, 25, 25) : Color.FromArgb(245, 245, 245);
        Renderer = SystemInformation.HighContrast ? new ToolStripSystemRenderer() : new MenuRenderer(light);
        foreach (ToolStripItem item in Items)
        {
            item.ForeColor = ForeColor;
            item.Padding = item is ToolStripSeparator ? Padding.Empty : new Padding(4, 4, 10, 4);
        }
        base.OnOpening(e);
        if (!SystemInformation.HighContrast)
        {
            int dark = light ? 0 : 1, corners = 3; // DWMWCP_ROUNDSMALL
            DwmSetWindowAttribute(Handle, 20, ref dark, sizeof(int));
            DwmSetWindowAttribute(Handle, 33, ref corners, sizeof(int));
        }
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) menuFont.Dispose();
    }

    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);

    sealed class MenuRenderer(bool light) : ToolStripRenderer
    {
        readonly Color background = light ? Color.FromArgb(249, 249, 249) : Color.FromArgb(43, 43, 43);
        readonly Color hover = light ? Color.FromArgb(234, 234, 234) : Color.FromArgb(62, 62, 62);
        readonly Color border = light ? Color.FromArgb(210, 210, 210) : Color.FromArgb(73, 73, 73);
        readonly Color muted = light ? Color.FromArgb(100, 100, 100) : Color.FromArgb(178, 178, 178);

        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e) => e.Graphics.Clear(background);
        protected override void OnRenderImageMargin(ToolStripRenderEventArgs e) { }
        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            using var pen = new Pen(border);
            e.Graphics.DrawRectangle(pen, 0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
        }
        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            if (!e.Item.Selected || !e.Item.Enabled) return;
            using var brush = new SolidBrush(hover);
            var r = new Rectangle(2, 1, e.Item.Width - 4, e.Item.Height - 2);
            using var path = new GraphicsPath();
            const int diameter = 7;
            path.AddArc(r.Left, r.Top, diameter, diameter, 180, 90);
            path.AddArc(r.Right - diameter, r.Top, diameter, diameter, 270, 90);
            path.AddArc(r.Right - diameter, r.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(r.Left, r.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.FillPath(brush, path);
        }
        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            // Draw directly: the stock disabled-text painter uses legacy embossing.
            TextRenderer.DrawText(e.Graphics, e.Text, e.TextFont, e.TextRectangle,
                e.Item.Enabled ? e.Item.ForeColor : muted, e.TextFormat);
        }
        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            using var pen = new Pen(border);
            e.Graphics.DrawLine(pen, 8, e.Item.Height / 2, e.Item.Width - 8, e.Item.Height / 2);
        }
        protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
        {
            var r = e.ImageRectangle;
            using var pen = new Pen(e.Item.ForeColor, 1.7f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.DrawLines(pen, new[] { new PointF(r.Left + r.Width * .2f, r.Top + r.Height * .5f), new PointF(r.Left + r.Width * .43f, r.Top + r.Height * .73f), new PointF(r.Left + r.Width * .82f, r.Top + r.Height * .27f) });
        }
    }
}
