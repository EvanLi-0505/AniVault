# AniVault

An offline-first personal anime & media library for Windows. Native WPF (.NET 10),
SQLite storage, no web components, no telemetry, no automatic network access.

> Status: offline library foundation + media detail page, episode tracking,
> **ZIP backup / restore**, and **Markdown export** all working. See
> [docs/STATUS.md](docs/STATUS.md) for details and [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)
> for the component conventions.

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
  Services/            Paths, settings, media CRUD, dialogs, navigation, logging
    Backup/            ZIP backup / restore
    Export/            Markdown library export (pluggable writers)
  ViewModels/          MVVM view models (CommunityToolkit.Mvvm)
  Views/               XAML views and dialog windows
  Controls/            Reusable UserControls (MediaCard, StatusBadge, RatingBadge, EmptyState)
  Resources/           Dark theme, control styles
  Utilities/           Converters, enum display labels, image loading
tests/AniVault.Tests/  xUnit tests for the offline business logic
build/                 Packaging scripts (publish, installer, icon)
dist/                  Build artifacts (git-ignored; go to GitHub Releases)
docs/                  Architecture & status notes
.github/workflows/     CI (build+test) and Release (tag v* -> artifacts)
```
