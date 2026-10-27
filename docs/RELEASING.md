# Publishing a release

1. Update `Version` in `CodexMinder.csproj` and add release notes in `docs/releases/VERSION.md`.
2. Run `scripts/package.ps1` on Windows with .NET 8 and Inno Setup 6.7.3. To install the pinned compiler into `.tools/inno`, run `scripts/setup-inno.ps1`.
3. Validate install, startup opt-out, update, and uninstall using a temporary directory and `/TASKS=""`. Exit the normal widget before testing; uninstall removes the CodexMinder startup entry, so restore your own setting afterward.
4. Commit and push. Tag the same commit `vVERSION` and push that tag.
5. The Windows build validates that the tag matches the project version, runs tests, packages the installer and portable ZIP, generates `SHA256SUMS.txt`, and creates a **draft** GitHub release containing all three assets. Inspect it and publish it when ready.

Normal pushes and pull requests build the same distributable files as Actions artifacts. Only tag jobs have release-write permission. Signing is not configured; do not describe the downloads as signed. The application runtime is bundled, and its license notices are copied from the exact runtime packs selected during restore.

Example local build with project-local tools:

```powershell
./scripts/package.ps1 -Dotnet "$PWD/.tools/dotnet/dotnet.exe" -Iscc "$PWD/.tools/inno/ISCC.exe" -ExpectedVersion v0.2.0
```
