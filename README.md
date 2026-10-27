# CodexMinder

A small Windows desktop companion displaying **remaining Codex allowances** over your taskbar:

```text
     Ø
78% / 87%
```

The first percentage is the five-hour allowance; the second is weekly. This is Codex subscription usage, not all ChatGPT model limits or API billing.

## Use

Run `CodexMinder.exe` from a permanent folder. On first launch it enables startup for your current Windows user. Right-click the display to change that setting, see reset times in your local timezone, refresh, switch between stacked and inline layouts, or exit. Drag the display to unused taskbar space; its position is saved. Double-click opens ChatGPT.

When ChatGPT or the Codex desktop application is running (including minimized), the percentages appear. When both are closed, only Ø appears. The percentages turn muted orange when either remaining allowance is strictly below 33%. There are no notifications or sounds.

The widget refreshes usage once per minute while either desktop app is running, checks process state every three seconds, and retries failed requests after 30 seconds. Missing windows or failed requests show dashes rather than invented or stale percentages. The context menu shows connection status and the last successful refresh time.

### Taskbar placement

This is an independent, topmost, non-activating window positioned **over** the taskbar, not an Explorer extension. It does not reserve taskbar space: drag it clear of your existing buttons. It can also be placed elsewhere on the desktop. It stays visible with taskbar auto-hide. The default location is on the primary taskbar; drag to another monitor if desired. The tray icon can restore it and supplies the same context menu. Use **Reset position** if your monitor layout changes.

The Ø mark is the requested placeholder, not an official OpenAI logo. The tray uses a Windows information icon in this first version.

## Authentication

Install Codex and sign in with your ChatGPT subscription account first. CodexMinder discovers the bundled `codex.exe` under `%LOCALAPPDATA%\OpenAI\Codex\bin`, then searches PATH. You can override discovery with the `CODEXMINDER_CODEX_PATH` environment variable pointing to an executable.

It starts a hidden, short-lived `codex app-server`, initializes the documented protocol, reads `account/rateLimits/read`, and closes the helper. It does not create threads, run model turns, or store credentials. It relies on the account already signed in to Codex. API-key-only authentication does not supply these subscription allowances. Read-only usage fetching was validated against a signed-in desktop installation.

Protocol reference: [OpenAI Codex App Server](https://learn.chatgpt.com/docs/app-server).

Settings are stored in `%LOCALAPPDATA%\CodexMinder\settings.json`. Startup uses the current user's `Software\Microsoft\Windows\CurrentVersion\Run\CodexMinder` registry value. No administrator privileges are required. Exit the app and disable startup before deleting or moving the executable; re-enable startup from its new location if you move it.

## Build and verify

Requires Windows 10/11 and the .NET 8 SDK. The published standalone build includes its runtime.

```powershell
dotnet build CodexMinder.csproj -c Release
dotnet run --project tests/CodexMinder.Tests.csproj
dotnet run --project tests/CodexMinder.Tests.csproj -- --live
dotnet publish CodexMinder.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o artifacts/CodexMinder
```

`--live` performs a real read-only account lookup. The other tests use fixtures and need no account or network. `CodexMinder.exe --demo` shows 80% / 88% without accessing your account or enabling startup.

GitHub Actions builds, runs fixture tests, and uploads a standalone Windows ZIP on pushes and pull requests.
