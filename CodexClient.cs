using System.Diagnostics;
using System.Text.Json;

namespace CodexMinder;

public static class CodexClient
{
    public static string FindExecutable()
    {
        var custom = Environment.GetEnvironmentVariable("CODEXMINDER_CODEX_PATH");
        if (!string.IsNullOrWhiteSpace(custom) && File.Exists(custom)) return custom;
        var bundled = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OpenAI", "Codex", "bin");
        if (Directory.Exists(bundled))
        {
            var match = Directory.GetFiles(bundled, "codex.exe", SearchOption.AllDirectories).OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
            if (match is not null) return match;
        }
        foreach (var directory in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
        {
            var candidate = Path.Combine(directory, "codex.exe");
            if (File.Exists(candidate)) return candidate;
        }
        throw new FileNotFoundException("Install Codex and sign in, or set CODEXMINDER_CODEX_PATH to codex.exe.");
    }

    public static async Task<Usage> ReadAsync(CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(25));
        var ct = timeout.Token;
        using var process = new Process { StartInfo = new(FindExecutable(), "app-server") {
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true,
            RedirectStandardOutput = true, RedirectStandardError = true
        }};
        process.Start();
        // Drain stderr without retaining potentially sensitive diagnostics.
        var drain = Task.Run(async () => { while (await process.StandardError.ReadLineAsync(ct) is not null) { } }, ct);
        try
        {
            await Send(new { id = 1, method = "initialize", @params = new { clientInfo = new { name = "codexminder", title = "CodexMinder", version = "0.1.0" } } });
            await Receive(1);
            await Send(new { method = "initialized", @params = new { } });
            await Send(new { id = 2, method = "account/rateLimits/read" });
            return Usage.Parse(await Receive(2));
        }
        finally
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync(CancellationToken.None);
            timeout.Cancel();
            try { await drain; } catch (OperationCanceledException) { }
        }

        async Task Send(object message) => await process.StandardInput.WriteLineAsync(JsonSerializer.Serialize(message).AsMemory(), ct);
        async Task<JsonElement> Receive(int id)
        {
            while (await process.StandardOutput.ReadLineAsync(ct) is { } line)
            {
                using var doc = JsonDocument.Parse(line);
                var root = doc.RootElement;
                if (!root.TryGetProperty("id", out var responseId) || !responseId.TryGetInt32(out var value) || value != id) continue;
                if (root.TryGetProperty("error", out var error)) throw new InvalidOperationException(error.TryGetProperty("message", out var msg) ? msg.GetString() : "Codex usage request failed.");
                return root.GetProperty("result").Clone();
            }
            throw new IOException("Codex app-server closed before returning usage.");
        }
    }
}
