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

static class Native
{
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr FindWindow(string name, string? title);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr window, out Rect rect);
    [StructLayout(LayoutKind.Sequential)] public struct Rect { public int Left, Top, Right, Bottom; }
}
