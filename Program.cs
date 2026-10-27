namespace CodexMinder;

internal static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        using var mutex = new Mutex(true, "Local\\CodexMinder", out var first);
        if (!first) return;
        ApplicationConfiguration.Initialize();
        Application.Run(new Widget(args.Contains("--demo"), args.Contains("--preview")));
    }
}
