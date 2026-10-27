# CodexMinder

A small Windows desktop companion displaying **remaining Codex allowances** over your taskbar:

## Install

Download the **Windows installer** or **portable ZIP** from [the latest release](https://github.com/erickolb/CodexMinder/releases/latest). The app includes its runtime; no .NET SDK or administrator access is required. Codex must already be installed and signed in with your ChatGPT subscription account. Current release binaries are unsigned.

See [installation, updates, uninstall, and agent setup](docs/INSTALL.md). You can also ask your agent: “Install and set up CodexMinder from https://github.com/erickolb/CodexMinder using the latest release and enable startup.”

## AI disclosure and license

CodexMinder was developed by Eric Kolb **with the assistance of OpenAI**, using Codex for implementation, testing, documentation, and packaging. It is an independent project and is not affiliated with or endorsed by OpenAI.

Source code is available under the [MIT License](LICENSE). The OpenAI logo and bundled third-party components retain their respective terms; see [third-party notices](THIRD-PARTY-NOTICES.md).

## Display

The OpenAI Blossom logo sits above the five-hour and weekly percentages (for example, `78% / 87%`), with an optional single-line layout.

The stacked layout occupies a 44 × 44 square at 100% display scaling. Its compact percentage line uses a 6.5-point font (slightly reduced when needed to fit `100%/100%`). The logo-only state has the same square footprint. Dimensions scale with Windows DPI settings. The optional inline layout remains wider.

The first percentage is the five-hour allowance; the second is weekly. This is Codex subscription usage, not all ChatGPT model limits or API billing.

## Use

The installer offers startup as a checkbox; portable first launch enables it by default. Run portable copies from a permanent folder. Right-click the display to change startup, see reset times in your local timezone, refresh, switch between stacked and inline layouts, or exit. Drag the display to unused taskbar space; its position is saved. Double-click opens ChatGPT.

When ChatGPT or the Codex desktop application is running (including minimized), the percentages appear. When both are closed, only the logo appears. Each percentage independently turns yellow below 50% remaining, orange below 33%, and red below 10%, with strict boundaries (50% is normal, 33% is yellow, 10% is orange). The slash and unaffected percentage retain the normal theme color. Yellow and red adjust for light/dark theme readability. There are no notifications or sounds.

The widget refreshes usage once per minute while either desktop app is running, checks process state every three seconds, and retries failed requests after 30 seconds. Refreshes and connection errors keep the last successful percentages on screen to avoid flicker; the context menu reports when it is showing last known values and the last successful refresh time. Dashes appear only before the first successful lookup or for an allowance the account does not report.

### Taskbar placement

The background is transparent: the taskbar's actual color, tint, and transparency show through, rather than a simulated solid panel. The logo and normal text follow Windows' system light/dark theme, updating within three seconds. Text and logo edges use alpha blending; the full widget area remains draggable and right-clickable.

The context menu follows the system light/dark theme each time it opens, with flat backgrounds, rounded hover highlights, themed checkmarks, and readable status text. Windows 11 supplies rounded popup corners where supported. High contrast uses Windows' system menu renderer and colors. This is a styled WinForms popup, not a WinUI menu.

Only an actual drag saves a fixed position; ordinary clicks leave automatic placement enabled. To clear an older saved position, choose **Reset position** or start with `--reset-position` after exiting the running instance. Taskbar bounds diagnostics are available via `dotnet run --project tests/CodexMinder.Tests.csproj -- --placement`.

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

GitHub Actions builds, runs fixture tests, and packages a standalone Windows ZIP, per-user installer, and SHA-256 manifest on pushes and pull requests. Version tags produce draft GitHub Releases for review. See [release maintenance](docs/RELEASING.md).
