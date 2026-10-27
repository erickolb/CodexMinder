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
    bool active, refreshing, dragging;
    Point dragStart;

    public Widget(bool demo, bool preview = false)
    {
        this.demo = demo;
        this.preview = preview;
        try { settings = JsonSerializer.Deserialize<Settings>(File.ReadAllText(settingsPath)) ?? new(); } catch { }
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = preview;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        DoubleBuffered = true;
        BackColor = Color.FromArgb(30, 30, 32);
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
        MouseDown += (_, e) => { if (e.Button == MouseButtons.Left) { dragging = true; dragStart = e.Location; Capture = true; } };
        MouseMove += (_, _) => { if (dragging) { var p = Cursor.Position; Location = new(p.X - dragStart.X, p.Y - dragStart.Y); } };
        MouseUp += (_, e) => { if (dragging) { dragging = false; Capture = false; settings.X = Left; settings.Y = Top; Save(); } if (e.Button == MouseButtons.Right) ShowMenu(Cursor.Position); };
        MouseDoubleClick += (_, _) => OpenChatGpt();
        timer.Tick += async (_, _) => await TickAsync();
        Shown += async (_, _) => { ResizeWidget(); Place(); if (!settings.StartupConfigured && !demo) { try { SetStartup(true); settings.StartupConfigured = true; Save(); } catch (Exception e) { status = e.Message; } } timer.Start(); await TickAsync(); };
    }

    protected override bool ShowWithoutActivation => true;
    protected override CreateParams CreateParams { get { var p = base.CreateParams; if (!preview) p.ExStyle |= 0x08000000 | 0x80; return p; } }

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
        if (demo) { usage = new(new(80, DateTimeOffset.Now.AddHours(2)), new(88, DateTimeOffset.Now.AddDays(4))); status = "Demo data"; }
        else if (active && !refreshing && DateTimeOffset.Now >= nextRefresh)
        {
            refreshing = true;
            try { usage = await CodexClient.ReadAsync(lifetime.Token); lastRefresh = DateTimeOffset.Now; status = "Connected"; nextRefresh = DateTimeOffset.Now.AddMinutes(1); }
            catch (Exception e) when (!lifetime.IsCancellationRequested) { usage = null; status = e is OperationCanceledException ? "Usage request timed out; retrying" : e.Message; nextRefresh = DateTimeOffset.Now.AddSeconds(30); }
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
        Invalidate();
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
        Size = new((int)((active ? settings.Stacked ? 108 : 144 : 40) * scale), (int)(44 * scale));
    }

    void Place(bool reset = false)
    {
        if (!reset && settings.X is int x && settings.Y is int y && Screen.AllScreens.Any(s => s.Bounds.Contains(x, y))) { Location = new(x, y); return; }
        var taskbar = FindWindow("Shell_TrayWnd", null);
        if (taskbar != IntPtr.Zero && GetWindowRect(taskbar, out var rect))
        {
            var gap = TaskbarPlacement.FindGap(taskbar, Rectangle.FromLTRB(rect.Left, rect.Top, rect.Right, rect.Bottom), new Size((int)(144 * DeviceDpi / 96f), Height));
            // Fall back just above the bar if it has no readable free space.
            Location = gap ?? new Point(rect.Right - Width - 12, rect.Top - Height - 4);
        }
        else { var area = Screen.PrimaryScreen!.WorkingArea; Location = new(area.Right - Width - 12, area.Bottom - Height - 8); }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var color = active && usage?.Low == true ? Color.FromArgb(213, 153, 87) : ForeColor;
        using var numbers = new Font("Segoe UI", settings.Stacked ? 10 : 11, FontStyle.Regular);
        var flags = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding;
        if (!active) OpenAiLogo.Draw(e.Graphics, blossom, new Rectangle(9, 9, Width - 18, Height - 18));
        else if (settings.Stacked)
        {
            var split = Height * 45 / 100;
            OpenAiLogo.Draw(e.Graphics, blossom, new Rectangle(0, 2, Width, split - 3));
            TextRenderer.DrawText(e.Graphics, usage?.Text ?? "— / —", numbers, new Rectangle(0, split, Width, Height - split), color, flags);
        }
        else
        {
            var split = Height;
            OpenAiLogo.Draw(e.Graphics, blossom, new Rectangle(8, 8, split - 16, Height - 16));
            TextRenderer.DrawText(e.Graphics, usage?.Text ?? "— / —", numbers, new Rectangle(split, 0, Width - split, Height), color, flags);
        }
    }

    void ShowMenu(Point point)
    {
        var menu = new ContextMenuStrip();
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
