using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Win32;

namespace CodexMinder;

public sealed class Widget : Form
{
    readonly System.Windows.Forms.Timer timer = new() { Interval = 3000 };
    readonly CancellationTokenSource lifetime = new();
    readonly NotifyIcon tray;
    readonly ToolTip tooltip = new();
    readonly System.Drawing.Drawing2D.GraphicsPath blossom = OpenAiLogo.Load();
    readonly bool demo;
    readonly bool preview;
    readonly string settingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CodexMinder", "settings.json");
    Settings settings = new();
    Usage? usage;
    string status = "Waiting for usage…";
    DateTimeOffset nextRefresh = DateTimeOffset.MinValue;
    DateTimeOffset nextPlacement = DateTimeOffset.MinValue;
    DateTimeOffset? lastRefresh;
    bool active, refreshing, dragging, moved;
    Point dragStart, dragOrigin;

    public Widget(bool demo, bool preview = false, bool resetPosition = false)
    {
        this.demo = demo;
        this.preview = preview;
        try { settings = JsonSerializer.Deserialize<Settings>(File.ReadAllText(settingsPath)) ?? new(); } catch { }
        if (resetPosition) { settings.X = settings.Y = null; Save(); }
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = preview;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        DoubleBuffered = true;
        ForeColor = Color.FromArgb(235, 235, 238);
        AccessibleName = "CodexMinder remaining Codex usage";
        Text = "CodexMinder";
        using (var bitmap = new Bitmap(32, 32))
        {
            using (var graphics = Graphics.FromImage(bitmap)) OpenAiLogo.Draw(graphics, blossom, new Rectangle(2, 2, 28, 28));
            var iconHandle = bitmap.GetHicon();
            using var temporaryIcon = Icon.FromHandle(iconHandle);
            Icon = (Icon)temporaryIcon.Clone();
            DestroyIcon(iconHandle);
        }
        tray = new NotifyIcon { Icon = Icon, Visible = true, Text = "CodexMinder" };
        tray.MouseClick += (_, e) => { if (e.Button == MouseButtons.Right) ShowMenu(Cursor.Position); else { TopMost = true; Show(); BringToFront(); } };
        MouseDown += (_, e) => { if (e.Button == MouseButtons.Left) { dragging = true; moved = false; dragStart = e.Location; dragOrigin = Location; Capture = true; } };
        MouseMove += (_, _) =>
        {
            if (!dragging) return;
            var p = Cursor.Position;
            moved |= Math.Abs(p.X - dragOrigin.X - dragStart.X) >= SystemInformation.DragSize.Width / 2 || Math.Abs(p.Y - dragOrigin.Y - dragStart.Y) >= SystemInformation.DragSize.Height / 2;
            if (moved) Location = new(p.X - dragStart.X, p.Y - dragStart.Y);
        };
        MouseUp += (_, e) => { if (dragging) { dragging = false; Capture = false; if (moved) { settings.X = Left; settings.Y = Top; Save(); } } if (e.Button == MouseButtons.Right) ShowMenu(Cursor.Position); };
        MouseDoubleClick += (_, _) => OpenChatGpt();
        timer.Tick += async (_, _) => await TickAsync();
        Shown += async (_, _) => { ResizeWidget(); Place(); if (!settings.StartupConfigured && !demo) { try { SetStartup(true); settings.StartupConfigured = true; Save(); } catch (Exception e) { status = e.Message; } } timer.Start(); await TickAsync(); };
    }

    protected override bool ShowWithoutActivation => true;
    protected override CreateParams CreateParams { get { var p = base.CreateParams; p.ExStyle |= 0x00080000; if (!preview) p.ExStyle |= 0x08000000 | 0x80; return p; } }

    async Task TickAsync()
    {
        active = demo || IsClientRunning();
        ResizeWidget();
        if (!dragging && settings.X is null && DateTimeOffset.Now >= nextPlacement)
        {
            Place();
            nextPlacement = DateTimeOffset.Now.AddSeconds(15);
        }
        if (!Screen.AllScreens.Any(s => s.Bounds.IntersectsWith(Bounds))) Place(reset: true);
        RenderWidget();
        if (demo) { usage = new(new(80, DateTimeOffset.Now.AddHours(2)), new(88, DateTimeOffset.Now.AddDays(4))); status = "Demo data"; }
        else if (active && !refreshing && DateTimeOffset.Now >= nextRefresh)
        {
            refreshing = true;
            try { usage = await CodexClient.ReadAsync(lifetime.Token); lastRefresh = DateTimeOffset.Now; status = "Connected"; nextRefresh = DateTimeOffset.Now.AddMinutes(1); }
            catch (Exception e) when (!lifetime.IsCancellationRequested) { status = (usage is null ? "" : "Showing last known values · ") + (e is OperationCanceledException ? "Usage request timed out; retrying" : e.Message); nextRefresh = DateTimeOffset.Now.AddSeconds(30); }
            catch (OperationCanceledException) { }
            finally { refreshing = false; }
        }
        if (IsDisposed) return;
        // Explorer can raise the taskbar above topmost windows after a click/restart.
        SetWindowPos(Handle, new IntPtr(-1), 0, 0, 0, 0, 0x1 | 0x2 | 0x10);
        var description = active ? $"5-hour / weekly remaining: {usage?.Text ?? "— / —"}\n{status}" : "ChatGPT and Codex are closed. Right-click to open ChatGPT.";
        tooltip.SetToolTip(this, description);
        AccessibleDescription = description;
        var trayText = active ? $"CodexMinder: {usage?.Text ?? "unavailable"}" : "CodexMinder: apps closed";
        tray.Text = trayText[..Math.Min(63, trayText.Length)];
        RenderWidget();
    }

    static bool IsClientRunning()
    {
        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                try
                {
                    var name = process.ProcessName;
                    if (name.Equals("ChatGPT", StringComparison.OrdinalIgnoreCase) || name.Equals("ChatGPT Desktop", StringComparison.OrdinalIgnoreCase)) return true;
                    // The app-server helper is codex.exe too: require a desktop window.
                    if (name.Equals("Codex", StringComparison.OrdinalIgnoreCase) && process.MainWindowHandle != IntPtr.Zero) return true;
                }
                catch (InvalidOperationException) { }
            }
        }
        return false;
    }

    void ResizeWidget()
    {
        var scale = DeviceDpi / 96f;
        Size = new((int)((active && !settings.Stacked ? 144 : 44) * scale), (int)(44 * scale));
    }

    void Place(bool reset = false)
    {
        if (!reset && settings.X is int x && settings.Y is int y && Screen.AllScreens.Any(s => s.Bounds.Contains(x, y))) { Location = new(x, y); return; }
        var taskbar = FindWindow("Shell_TrayWnd", null);
        if (taskbar != IntPtr.Zero && GetWindowRect(taskbar, out var rect))
        {
            var gap = TaskbarPlacement.FindGap(taskbar, Rectangle.FromLTRB(rect.Left, rect.Top, rect.Right, rect.Bottom), Size);
            // Fall back just above the bar if it has no readable free space.
            Location = gap ?? new Point(rect.Right - Width - 12, rect.Top - Height - 4);
        }
        else { var area = Screen.PrimaryScreen!.WorkingArea; Location = new(area.Right - Width - 12, area.Bottom - Height - 8); }
    }

    protected override void OnPaintBackground(PaintEventArgs e) { }
    protected override void OnPaint(PaintEventArgs e) => RenderWidget();

    void RenderWidget()
    {
        if (!IsHandleCreated || IsDisposed || Width <= 0 || Height <= 0) return;
        bool light = SystemTheme.IsLight;
        ForeColor = SystemInformation.HighContrast ? SystemColors.WindowText : light ? Color.FromArgb(28, 28, 30) : Color.FromArgb(235, 235, 238);
        using var bitmap = new Bitmap(Width, Height, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
        bitmap.SetResolution(DeviceDpi, DeviceDpi);
        using var graphics = Graphics.FromImage(bitmap);
        // One alpha step keeps the full rectangle draggable/right-clickable while
        // allowing the actual taskbar material beneath it to remain visible.
        graphics.Clear(Color.FromArgb(1, 0, 0, 0));
        graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
        var color = (active ? usage?.Level : UsageLevel.Normal) switch
        {
            UsageLevel.Yellow => light ? Color.FromArgb(145, 110, 12) : Color.FromArgb(220, 193, 92),
            UsageLevel.Orange => Color.FromArgb(213, 153, 87),
            UsageLevel.Red => light ? Color.FromArgb(186, 45, 45) : Color.FromArgb(230, 106, 106),
            _ => ForeColor
        };
        using var brush = new SolidBrush(color);
        using var format = new StringFormat(StringFormat.GenericTypographic) { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        var text = usage?.Text ?? "— / —";
        if (settings.Stacked) text = text.Replace(" / ", "/");
        using var preferredFont = new Font("Segoe UI", settings.Stacked ? 6.5f : 11, FontStyle.Regular);
        var measured = graphics.MeasureString(text, preferredFont, int.MaxValue, format).Width;
        var available = Width - 2 * DeviceDpi / 96f;
        // Keep even 100%/100% on one line within the square, at any display scale.
        using var numbers = new Font("Segoe UI", preferredFont.Size * (settings.Stacked ? Math.Min(1, available / measured) : 1), FontStyle.Regular);
        format.FormatFlags |= StringFormatFlags.NoWrap;
        if (!active) OpenAiLogo.Draw(graphics, blossom, new Rectangle(9, 9, Width - 18, Height - 18), ForeColor);
        else if (settings.Stacked)
        {
            var split = Height * 65 / 100;
            var logoSize = (int)((split - 3) * .75f);
            OpenAiLogo.Draw(graphics, blossom, new Rectangle((Width - logoSize) / 2, 2 + (split - 3 - logoSize) / 2, logoSize, logoSize), ForeColor);
            graphics.DrawString(text, numbers, brush, new RectangleF(0, split, Width, Height - split), format);
        }
        else
        {
            var split = Height;
            OpenAiLogo.Draw(graphics, blossom, new Rectangle(8, 8, split - 16, Height - 16), ForeColor);
            graphics.DrawString(usage?.Text ?? "— / —", numbers, brush, new RectangleF(split, 0, Width - split, Height), format);
        }
        LayeredSurface.Present(Handle, bitmap, Location);
    }

    void ShowMenu(Point point)
    {
        var menu = new ThemedMenu();
        menu.Items.Add("Codex usage · remaining").Enabled = false;
        menu.Items.Add($"5-hour: {Reset(usage?.FiveHour)}").Enabled = false;
        menu.Items.Add($"Weekly: {Reset(usage?.Weekly)}").Enabled = false;
        menu.Items.Add(status).Enabled = false;
        if (lastRefresh is { } updated) menu.Items.Add($"Updated {updated.LocalDateTime:t}").Enabled = false;
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Open ChatGPT", null, (_, _) => OpenChatGpt());
        menu.Items.Add("Open Codex", null, (_, _) => OpenDesktopApp("Codex"));
        menu.Items.Add("Refresh now", null, async (_, _) => { nextRefresh = DateTimeOffset.MinValue; await TickAsync(); });
        var stacked = new ToolStripMenuItem("Logo above percentages") { Checked = settings.Stacked };
        stacked.Click += (_, _) => { settings.Stacked = !settings.Stacked; Save(); ResizeWidget(); Invalidate(); };
        menu.Items.Add(stacked);
        var startup = new ToolStripMenuItem("Run at Windows startup") { Checked = StartupEnabled() };
        startup.Click += (_, _) => { try { SetStartup(!StartupEnabled()); } catch (Exception e) { MessageBox.Show(e.Message, "Startup setting"); } };
        menu.Items.Add(startup);
        menu.Items.Add("Reset position (drag to move)", null, (_, _) => { settings.X = settings.Y = null; Save(); Place(true); });
        menu.Items.Add("Exit", null, (_, _) => Close());
        menu.Closed += (_, _) => { if (!IsDisposed && IsHandleCreated) BeginInvoke(() => menu.Dispose()); };
        // A non-activating overlay must explicitly foreground itself for standard
        // popup-menu outside-click dismissal (the same rule as tray menus).
        SetForegroundWindow(Handle);
        menu.Show(point);
        PostMessage(Handle, 0, IntPtr.Zero, IntPtr.Zero);
    }

    static string Reset(Quota? q) => q is null ? "unavailable" : q.Reset is { } r ? $"{Math.Floor(q.Remaining):0}% · resets {r.LocalDateTime:ddd, MMM d, h:mm tt}" : $"{Math.Floor(q.Remaining):0}% · reset unknown";
    static void OpenChatGpt() => OpenDesktopApp("ChatGPT");
    static void OpenDesktopApp(string name)
    {
        // Store app identities vary across releases; ask the Windows Apps folder.
        object? shell = null, folder = null, items = null;
        try
        {
            shell = Activator.CreateInstance(Type.GetTypeFromProgID("Shell.Application")!);
            folder = ((dynamic)shell!).NameSpace("shell:AppsFolder");
            items = ((dynamic)folder!).Items();
            foreach (var item in (dynamic)items)
            {
                try
                {
                    string label = item.Name;
                    string path = item.Path;
                    if (label.Equals(name, StringComparison.OrdinalIgnoreCase))
                    {
                        Process.Start(new ProcessStartInfo("explorer.exe") { Arguments = $"shell:AppsFolder\\{path}", UseShellExecute = true });
                        return;
                    }
                }
                finally { if (Marshal.IsComObject(item)) Marshal.ReleaseComObject(item); }
            }
        }
        catch (Exception) { /* Fall back to the web app when not installed. */ }
        finally
        {
            foreach (var value in new[] { items, folder, shell }) if (value is not null && Marshal.IsComObject(value)) Marshal.ReleaseComObject(value);
        }
        Launch(name == "Codex" ? "https://chatgpt.com/codex" : "https://chatgpt.com");
    }
    static void Launch(string target, string? fallback = null)
    {
        try { Process.Start(new ProcessStartInfo(target) { UseShellExecute = true }); }
        catch (Exception e) { if (fallback is not null) Launch(fallback); else MessageBox.Show(e.Message, "CodexMinder"); }
    }
    static bool StartupEnabled() { using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run"); return key?.GetValue("CodexMinder") is string; }
    static void SetStartup(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
        if (enabled) key.SetValue("CodexMinder", $"\"{Environment.ProcessPath}\""); else key.DeleteValue("CodexMinder", false);
    }
    void Save() { Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!); File.WriteAllText(settingsPath, JsonSerializer.Serialize(settings)); }
    protected override void Dispose(bool disposing)
    {
        if (disposing) { lifetime.Cancel(); timer.Dispose(); tray.Dispose(); tooltip.Dispose(); blossom.Dispose(); Icon?.Dispose(); }
        base.Dispose(disposing);
    }
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr FindWindow(string className, string? title);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr window, out Rect rect);
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr window);
    [DllImport("user32.dll")] static extern bool PostMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr icon);
    [StructLayout(LayoutKind.Sequential)] struct Rect { public int Left, Top, Right, Bottom; }
    public class Settings { public int? X { get; set; } public int? Y { get; set; } public bool Stacked { get; set; } = true; public bool StartupConfigured { get; set; } }
}
