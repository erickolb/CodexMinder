namespace CodexMinder;

internal static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        if (args.Contains("--exit"))
        {
            if (EventWaitHandle.TryOpenExisting("Local\\CodexMinder.Exit", out var running))
                using (running) running.Set();
            return;
        }
        using var mutex = new Mutex(true, "Local\\CodexMinder", out var first);
        if (!first) return;
        using var shutdown = new EventWaitHandle(false, EventResetMode.AutoReset, "Local\\CodexMinder.Exit");
        ApplicationConfiguration.Initialize();
        using var widget = new Widget(args.Contains("--demo"), args.Contains("--preview"), args.Contains("--reset-position"));
        var registration = ThreadPool.RegisterWaitForSingleObject(shutdown, (_, _) =>
        {
            if (widget.IsHandleCreated && !widget.IsDisposed)
                try { widget.BeginInvoke(() => widget.Close()); } catch (InvalidOperationException) { }
        }, null, Timeout.Infinite, false);
        try { Application.Run(widget); }
        finally { registration.Unregister(null); }
    }
}
