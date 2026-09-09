# dist/ — distribution artifacts

The build scripts write release artifacts here. **The binaries are not committed to git**
(see `.gitignore` — only this file is). Attach them to a GitHub Release instead.

## What each file is

| File | What it is |
|---|---|
| `AniVault-<version>-win-x64-portable.zip` | **Portable build (primary).** Extract, run `AniVault/AniVault.exe`. No install, no .NET needed. On first launch it asks where to keep your library data. |
| `AniVault-<version>-win-x64-portable/` | The same, already unzipped (local convenience). |
| `AniVault-<version>-Setup.exe` | Optional per-user installer (Inno Setup). No admin prompt, Start-menu shortcut, clean uninstall. Never touches your data folder. |
| `SHA256SUMS.txt` | Plain-text SHA-256 checksums of the `.zip` and `Setup.exe`, one per line. Lets anyone verify a download wasn't corrupted or tampered with (`Get-FileHash file.zip` and compare). Safe to ignore if you don't need it. |

## Build

```powershell
pwsh build/publish.ps1          # -> portable zip + exe
pwsh build/make-installer.ps1   # -> Setup.exe
```

Version comes from `<Version>` in `src/AniVault/AniVault.csproj`.

## Release checklist

1. Bump `<Version>` in `src/AniVault/AniVault.csproj`.
2. Run both scripts above.
3. `git tag v<version> && git push --tags` — the `release.yml` workflow builds and attaches
   `dist/*.zip`, `dist/*.exe` and `SHA256SUMS.txt` to a new GitHub Release automatically.
