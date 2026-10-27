# Install, update, and uninstall

## Requirements

- Windows 10/11 on x64. The x64 build may also run through Windows' emulation on ARM64, but that has not been validated.
- Codex installed and signed in with a ChatGPT subscription account. CodexMinder uses that existing account; API-key-only sign-in does not provide subscription allowances.
- No .NET download, compiler, administrator account, or API key is needed for the packaged app.

## Recommended: installer

1. Open [the latest release](https://github.com/erickolb/CodexMinder/releases/latest).
2. Download `CodexMinder-VERSION-win-x64-setup.exe` (not GitHub's source-code ZIP).
3. Run it as your normal Windows user. The default location is `%LOCALAPPDATA%\Programs\CodexMinder`.
4. Choose whether to run at Windows sign-in, then finish with **Launch CodexMinder** checked.

The installer creates a Start menu shortcut and an entry in Windows Installed apps. It does not sign in to OpenAI or request your credentials. Startup can also be changed from the widget's context menu. Installation does not automatically opt you back into startup if you unchecked it.

The first release is **unsigned**. Windows may show an unknown-publisher/reputation warning, or local security policy may block it. Review the source and release provenance according to your own policy; installation does not require disabling Windows security features. SHA-256 checksums are published with every release for checking download integrity, not as a substitute for publisher signing.

## Portable ZIP

Download `CodexMinder-VERSION-win-x64.zip` from the release and extract it into a permanent directory that you can write to. Keep the included license and notice files with the app. Run `CodexMinder.exe`. Portable first launch enables startup by default; disable it in the context menu if desired. Do not launch from inside the ZIP or leave it in a temporary directory.

## Agent-assisted installation

You can ask an agent:

> Install and set up CodexMinder from https://github.com/erickolb/CodexMinder. Follow docs/INSTALL.md, use the latest published release, and enable startup.

For agents:

1. Read this file and retrieve the latest non-prerelease release from the exact `erickolb/CodexMinder` repository, using GitHub's API or `gh release view --repo erickolb/CodexMinder --json tagName,assets`. If the repository is private, use the user's existing authorized GitHub connection.
2. Check that Windows x64 is suitable and that Codex is installed and signed in. If authentication is missing, ask the user to sign in themselves. Do not read or copy credential files.
3. Download the release installer and `SHA256SUMS.txt`. Verify the selected file's SHA-256 with `Get-FileHash` against its exact filename in the manifest. Stop on mismatch. Do not substitute artifacts from a fork or use the source-code archive as the installer.
4. Run the installer as the current user. Interactive setup is preferred. For an explicitly authorized unattended installation, use `/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /TASKS="startup"` (or `/TASKS=""` to opt out). Wait for exit and stop on failure. Respect host execution/approval requirements. Do not bypass security warnings or automatically install a build toolchain.
5. Silent installation does not launch the app. Start `%LOCALAPPDATA%\Programs\CodexMinder\CodexMinder.exe` and verify that the process remains running. Have the user check the context menu for a successful refresh. Unknown allowances may display dashes; do not treat them as zero.
6. Explain the location, startup setting, and uninstall steps. Do not make unrelated changes to Windows or the Codex account.

## Updating

Download and run the new version's installer under the same Windows account and retain the same install folder. The installer requests a graceful shutdown of an installed running copy and preserves widget preferences. If an older portable copy is running, exit it first. Startup task selection is remembered across installer upgrades; explicitly selecting it changes the setting.

For a portable copy, right-click **Exit**, replace the extracted files with the new release, and run it again. Preserve `settings.json` in `%LOCALAPPDATA%\CodexMinder`. If changing directories, disable startup from the old copy and re-enable it from the new copy.

There is no automatic updater; use [GitHub Releases](https://github.com/erickolb/CodexMinder/releases) to check for updates.

## Uninstalling

For the installer, use **Settings → Apps → Installed apps → CodexMinder → Uninstall**. It stops the installed copy and removes installed files, the shortcut, and the startup entry. It preserves `%LOCALAPPDATA%\CodexMinder\settings.json` so reinstalling retains preferences; delete that folder yourself if you also want to remove preferences. Codex account credentials are never removed.

For a portable copy, disable **Run at Windows startup**, choose **Exit**, and delete the extracted folder.

## Troubleshooting

- No values: ensure Codex is signed in to the intended ChatGPT subscription account. The context menu shows the current error and last successful update. Requests normally refresh every minute.
- Codex not found: set `CODEXMINDER_CODEX_PATH` to the installed `codex.exe` and restart the widget.
- Poor placement: use **Reset position** to restore automatic placement, or drag to choose a fixed location. After exiting, `CodexMinder.exe --reset-position` also clears it.
- Another copy is running: launch it from the tray or use `CodexMinder.exe --exit` for a graceful shutdown before reinstalling.
