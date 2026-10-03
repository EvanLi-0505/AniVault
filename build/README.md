# build/ — packaging scripts

| Script | Purpose |
|---|---|
| `make-icon.ps1` | Regenerates `src/AniVault/Resources/Icons/AniVault.ico` from code (no external tools). Only needs re-running if the icon design changes; the `.ico` is committed. |
| `publish.ps1` | `dotnet publish` → self-contained, single-file, compressed **win-x64** build. Stages `dist/AniVault-<version>-win-x64-portable/AniVault/` (`AniVault.exe` + `README.txt`) and zips it. Writes `dist/SHA256SUMS.txt`. |
| `make-installer.ps1` | Compiles `installer/AniVault.iss` with Inno Setup 6 → `dist/AniVault-<version>-Setup.exe`. Installs Inno Setup via `winget` if it is not present. Run `publish.ps1` first. |
| `installer/AniVault.iss` | Inno Setup script. Per-user install (no admin prompt), Start-menu shortcut, optional desktop icon. Installs exactly what the ZIP contains (`AniVault.exe` + `README.txt`) — deliberately **no uninstaller** and no "Installed apps" entry; removing the app is deleting the folder. Never touches the user's chosen data folder. |

## Requirements

- .NET 10 SDK
- Windows (WPF + single-file publish are Windows-only)
- Inno Setup 6 — only for `make-installer.ps1`; auto-installed via winget if missing

## Typical flow

```powershell
# from the repo root
pwsh build/publish.ps1
pwsh build/make-installer.ps1
```

Artifacts land in `dist/` (git-ignored — upload them to a GitHub Release).

## Notes

- `PublishTrimmed` is **off**: trimming is unsupported for WPF.
- The build is `--self-contained`, so the target machine needs no .NET install; this is
  why `AniVault.exe` is ~65 MB.
- Version comes from `<Version>` in `src/AniVault/AniVault.csproj` — bump it there.
- The portable pointer file `anivault.config.json` sits next to the exe when that folder is
  writable, otherwise the app falls back to `%LOCALAPPDATA%\AniVault\bootstrap.json`
  (e.g. when installed under Program Files).
