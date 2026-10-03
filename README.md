# AniVault

An offline-first personal anime & media library for Windows. Native WPF (.NET 10),
SQLite storage, no web components, no telemetry, no automatic network access.

> Status: 2.0 — full local library (CRUD, episodes with a per-title hideable checklist, ratings
> with a guided questionnaire, always-editable category, tags with drag-to-reorder, anime
> seasons, artwork with an explicit opt-in compression tool), paged poster grids that always
> end on a full row with jump-to-page and a collapsible filter panel, optional online metadata
> search (Bangumi / AniList / Jikan / Kitsu / TMDB / one custom provider) with duplicate
> detection and multi-select batch import, ZIP backup/restore (older backups are upgraded
> automatically), Markdown export, single-instance guard, portable packaging + installer,
> light/dark themes, and full English / 中文 localization. See
> [docs/STATUS.md](docs/STATUS.md) for the authoritative feature list and
> [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) for the component conventions.

## Download & use

Grab the latest build from the [Releases](../../releases) page — no .NET install needed:

- **`AniVault-<version>-win-x64-portable.zip`** — extract anywhere and run `AniVault.exe`.
- **`AniVault-<version>-Setup.exe`** — per-user installer (no admin prompt) that adds a
  Start-menu shortcut. It installs the same two files as the ZIP and no uninstaller.

On first launch you choose a data folder; the database, artwork, logs and backups all live
there. To update, replace `AniVault.exe` with the new one — your data folder is untouched.

**Removing AniVault** — delete the app folder (plus the shortcut if you used the installer)
and, if you want the library gone too, your data folder. The only other thing left on the PC is
a small runtime cache at `%TEMP%\.net\AniVault` (about 10 MB; Settings → Data folder has a
button that opens it). AniVault writes nothing to the registry.

## Requirements

- Windows 10/11 x64
- .NET 10 SDK (to build) — <https://dotnet.microsoft.com/download/dotnet/10.0>

## Build & run

```bash
dotnet build AniVault.slnx
dotnet run --project src/AniVault
```

On first launch AniVault asks you to choose a data folder (e.g. `D:\AniVaultData`).
Everything — database, artwork, logs, backups — lives there, so the app stays portable.

## Test

```bash
dotnet test AniVault.slnx
```

## Package

```powershell
pwsh build/publish.ps1          # portable single-file zip  -> dist/
pwsh build/make-installer.ps1   # optional Setup.exe         -> dist/
```

`dist/` artifacts are git-ignored; attach them to a GitHub Release (a tag `v*` does this
automatically via `.github/workflows/release.yml`). See [dist/README.md](dist/README.md).

## Project layout

```
AniVault.slnx
src/AniVault/          WPF application (net10.0-windows)
  Models/              Entities + enums
  Data/                DbContext, EF configurations, migrations, initializer
  Services/            Paths, settings, media/tag CRUD, dialogs, navigation, logging,
                       single-instance guard, theme + localization services
    Backup/            ZIP backup / restore
    Export/            Markdown library export (pluggable writers)
  Metadata/            The only network code — online providers (Bangumi/AniList/Jikan/
                       Kitsu/TMDB/custom), search + import + duplicate detection, gated
                       behind an explicit button and a settings switch (never automatic)
  Composition/         DI registration, one Add<X>Services extension method per subsystem
  ViewModels/          MVVM view models (CommunityToolkit.Mvvm)
  Views/               XAML views and dialog windows
  Controls/            Reusable UserControls (MediaCard, MediaRow, StatusBadge, RatingBadge,
                       RatingStars, EmptyState, TagChip, FilterPanel)
  Resources/           Light/dark themes, control styles, en/zh string tables, rating rubric
  Utilities/           Converters, enum display labels, image loading, markdown rendering
tests/AniVault.Tests/  xUnit tests for the offline business logic
build/                 Packaging scripts (publish, installer, icon)
dist/                  Build artifacts (git-ignored; go to GitHub Releases)
docs/                  Architecture & status notes
.github/workflows/     CI (build+test) and Release (tag v* -> artifacts)
```
