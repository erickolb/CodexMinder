using System.Text.Json;
using CodexMinder;
using System.Runtime.InteropServices;

if (args.Contains("--placement"))
{
    Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
    var taskbar = Native.FindWindow("Shell_TrayWnd", null);
    Native.GetWindowRect(taskbar, out var r);
    Console.WriteLine($"Taskbar {r.Left}, {r.Top}, {r.Right}, {r.Bottom}");
    Console.WriteLine(TaskbarPlacement.FindGap(taskbar, Rectangle.FromLTRB(r.Left, r.Top, r.Right, r.Bottom), new Size(44,44), Console.WriteLine));
    return;
}

if (args.Contains("--live"))
{
    var live = await CodexClient.ReadAsync(CancellationToken.None);
    Console.WriteLine($"Live remaining: {live.Text}; five-hour reset: {live.FiveHour?.Reset}; weekly reset: {live.Weekly?.Reset}");
    return;
}

Usage Parse(string json) { using var doc = JsonDocument.Parse(json); return Usage.Parse(doc.RootElement); }
void Check(bool condition, string description) { if (!condition) throw new Exception(description); Console.WriteLine($"PASS {description}"); }
var usage = Parse("""{"rateLimitsByLimitId":{"codex":{"primary":{"usedPercent":20,"windowDurationMins":300,"resetsAt":1793124107},"secondary":{"usedPercent":12,"windowDurationMins":10080,"resetsAt":1793650862}}}}""");
Check(usage.Text == "80% / 88%" && !usage.Low, "Remaining percentages and window mapping");
Check(usage.FiveHour?.Reset == DateTimeOffset.FromUnixTimeSeconds(1793124107), "Unix reset timestamp");
var low = Parse("""{"rateLimits":{"primary":{"usedPercent":67.1,"windowDurationMins":300},"secondary":{"usedPercent":0,"windowDurationMins":10080}}}""");
Check(low.Low && low.Text == "32% / 100%", "Strictly below 33%, conservatively rounded");
var boundary = Parse("""{"rateLimits":{"primary":{"usedPercent":67,"windowDurationMins":300}}}""");
Check(!boundary.Low && boundary.Text == "33% / —", "33% is not low; missing weekly remains unknown");
var weekly = Parse("""{"rateLimits":{"primary":{"usedPercent":90,"windowDurationMins":10080},"secondary":null}}""");
Check(weekly.Low && weekly.Text == "— / 10%", "Weekly-only plan does not masquerade as five-hour");
var other = Parse("""{"rateLimits":{"primary":{"usedPercent":1,"windowDurationMins":15}}}""");
Check(other.Text == "— / —", "Unrelated window durations are not mislabeled");
var clamped = Parse("""{"rateLimits":{"primary":{"usedPercent":120,"windowDurationMins":300},"secondary":{"usedPercent":-1,"windowDurationMins":10080}}}""");
Check(clamped.Text == "0% / 100%", "Out-of-range percentages clamp");
var preferred = Parse("""{"rateLimits":{"primary":{"usedPercent":99,"windowDurationMins":300}},"rateLimitsByLimitId":{"codex":{"primary":{"usedPercent":20,"windowDurationMins":300}}}}""");
Check(preferred.FiveHour?.Remaining == 80, "Explicit Codex bucket takes precedence");

foreach (var (remaining, expected) in new[] {
    (100d, UsageLevel.Normal), (50d, UsageLevel.Normal), (49.9, UsageLevel.Yellow),
    (33d, UsageLevel.Yellow), (32.9, UsageLevel.Orange), (10d, UsageLevel.Orange),
    (9.9, UsageLevel.Red), (0d, UsageLevel.Red) })
{
    Check(new Usage(new(remaining, null), new(100, null)).Level == expected, $"Five-hour threshold at {remaining}%");
    Check(new Usage(new(100, null), new(remaining, null)).Level == expected, $"Weekly threshold at {remaining}%");
    Check(new Quota(remaining, null).Level == expected, $"Independent percentage threshold at {remaining}%");
}
Check(new Usage(new(49, null), new(9, null)).Level == UsageLevel.Red, "Most urgent allowance determines color");
Check(new Usage(null, null).Level == UsageLevel.Normal, "Unknown allowances do not trigger warning colors");
var independent = new Usage(new(46, null), new(82, null));
Check(independent.FiveHour!.Level == UsageLevel.Yellow && independent.Weekly!.Level == UsageLevel.Normal, "46% is yellow while 82% stays normal");

Check(TrayArtwork.StatusFor(null) == TrayStatus.Unknown && TrayArtwork.StatusFor(new(null, null)) == TrayStatus.Unknown, "Tray is blue only when both allowances are unknown");
foreach (var (remaining, expected) in new[] { (82d, TrayStatus.Green), (46d, TrayStatus.Yellow), (22d, TrayStatus.Orange), (5d, TrayStatus.Red) })
{
    Check(TrayArtwork.StatusFor(new(new(remaining, null), null)) == expected, $"Known five-hour {remaining}% determines tray status with unknown weekly");
    Check(TrayArtwork.StatusFor(new(null, new(remaining, null))) == expected, $"Known weekly {remaining}% determines tray status with unknown five-hour");
}
Check(TrayArtwork.StatusFor(new(new(46, null), new(82, null))) == TrayStatus.Yellow, "46% / 82% gives yellow badge");
Check(TrayArtwork.StatusFor(new(new(46, null), new(5, null))) == TrayStatus.Red, "Red takes priority over yellow for tray badge");
using (var logo = OpenAiLogo.Load())
foreach (var light in new[] { false, true })
foreach (var size in new[] { 16, 24, 40 })
foreach (var state in Enum.GetValues<TrayStatus>())
{
    using var bitmap = TrayArtwork.Render(logo, state, light, size);
    Check(bitmap.GetPixel(0, 0).A == 0, $"Transparent tray corner at {size}px, {state}, light={light}");
    var color = TrayArtwork.BadgeColor(state, light);
    bool colored = false;
    for (int y = (int)(size * .45); y < size; y++)
    for (int x = (int)(size * .45); x < size; x++)
    {
        var pixel = bitmap.GetPixel(x, y);
        if (pixel.A > 50 && Math.Abs(pixel.R - color.R) < 8 && Math.Abs(pixel.G - color.G) < 8 && Math.Abs(pixel.B - color.B) < 8) colored = true;
    }
    Check(colored, $"Visible colored percent at {size}px, {state}, light={light}");
    using var icon = TrayArtwork.Create(logo, state, light, size);
    Check(icon.Width == size && icon.Height == size, $"Native tray icon size {size}px, {state}, light={light}");
}

static class Native
{
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr FindWindow(string name, string? title);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr window, out Rect rect);
    [StructLayout(LayoutKind.Sequential)] public struct Rect { public int Left, Top, Right, Bottom; }
}
