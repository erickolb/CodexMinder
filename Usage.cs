using System.Text.Json;

namespace CodexMinder;

public record Quota(double Remaining, DateTimeOffset? Reset);
public enum UsageLevel { Normal, Yellow, Orange, Red }
public record Usage(Quota? FiveHour, Quota? Weekly)
{
    public UsageLevel Level
    {
        get
        {
            var remaining = Math.Min(FiveHour?.Remaining ?? 100, Weekly?.Remaining ?? 100);
            return remaining < 10 ? UsageLevel.Red : remaining < 33 ? UsageLevel.Orange : remaining < 50 ? UsageLevel.Yellow : UsageLevel.Normal;
        }
    }
    public bool Low => FiveHour?.Remaining < 33 || Weekly?.Remaining < 33;
    public string Text => $"{Format(FiveHour)} / {Format(Weekly)}";
    static string Format(Quota? q) => q is null ? "—" : $"{Math.Floor(q.Remaining):0}%";

    public static Usage Parse(JsonElement result)
    {
        JsonElement bucket;
        if (result.TryGetProperty("rateLimitsByLimitId", out var map) && map.ValueKind == JsonValueKind.Object && map.TryGetProperty("codex", out var codex)) bucket = codex;
        else if (!result.TryGetProperty("rateLimits", out bucket)) throw new InvalidDataException("No Codex usage bucket returned.");
        Quota? five = null, week = null;
        foreach (var name in new[] { "primary", "secondary" })
        {
            if (!bucket.TryGetProperty(name, out var window) || window.ValueKind != JsonValueKind.Object) continue;
            if (!window.TryGetProperty("windowDurationMins", out var duration) || !duration.TryGetInt32(out var minutes)) continue;
            if (!window.TryGetProperty("usedPercent", out var percent) || !percent.TryGetDouble(out var used) || !double.IsFinite(used)) continue;
            DateTimeOffset? reset = null;
            if (window.TryGetProperty("resetsAt", out var time) && time.TryGetInt64(out var seconds))
            {
                try { reset = DateTimeOffset.FromUnixTimeSeconds(seconds); } catch (ArgumentOutOfRangeException) { }
            }
            var quota = new Quota(Math.Clamp(100 - used, 0, 100), reset);
            if (minutes == 300) five = quota;
            if (minutes == 10080) week = quota;
        }
        return new(five, week);
    }
}
