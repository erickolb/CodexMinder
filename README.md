# CodexMinder

A small Windows desktop companion displaying **remaining Codex allowances** over your taskbar:

The OpenAI Blossom logo sits above the five-hour and weekly percentages (for example, `78% / 87%`), with an optional single-line layout.

The first percentage is the five-hour allowance; the second is weekly. This is Codex subscription usage, not all ChatGPT model limits or API billing.

## Use

Run `CodexMinder.exe` from a permanent folder. On first launch it enables startup for your current Windows user. Right-click the display to change that setting, see reset times in your local timezone, refresh, switch between stacked and inline layouts, or exit. Drag the display to unused taskbar space; its position is saved. Double-click opens ChatGPT.

When ChatGPT or the Codex desktop application is running (including minimized), the percentages appear. When both are closed, only the logo appears. The percentages turn muted orange when either remaining allowance is strictly below 33%. There are no notifications or sounds.

The widget refreshes usage once per minute while either desktop app is running, checks process state every three seconds, and retries failed requests after 30 seconds. Missing windows or failed requests show dashes rather than invented or stale percentages. The context menu shows connection status and the last successful refresh time.

### Taskbar placement

The background is transparent: the taskbar's actual color, tint, and transparency show through, rather than a simulated solid panel. The logo and normal text follow Windows' system light/dark theme, updating within three seconds. Text and logo edges use alpha blending; the full widget area remains draggable and right-clickable.

This is an independent, topmost, non-activating window positioned **over** the taskbar, not an Explorer extension. Automatic placement reads the taskbar's accessible controls and chooses a free gap, checking again every 15 seconds as buttons change. If no suitable gap is available, it sits just above the taskbar rather than covering buttons. Dragging sets a fixed position; **Reset position** restores automatic placement. It does not reserve taskbar space. It can also be placed elsewhere on the desktop and stays visible with taskbar auto-hide. Automatic placement uses the primary taskbar; drag to another monitor if desired. The tray icon can restore it and supplies the same context menu.

The display and tray use OpenAI's Blossom mark to identify the service being monitored. The original SVG is retained in `Assets/Blossom_Light.svg`; the widget renders its filled logo path without the construction guides. Source: [OpenAI design guidelines](https://openai.com/brand/) and its [official SVG asset](https://images.ctfassets.net/kftzwdyauwt9/3hUGLn3ypllZ0oa01qOYVq/28e8188e6f11b84c3e876569d492734f/Blossom_Light.svg). OpenAI owns the mark. CodexMinder is an independent companion and is not endorsed by OpenAI.

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
