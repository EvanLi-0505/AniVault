# AniVault architecture & component conventions

This document is the contract for anyone (human or AI) extending AniVault. Follow it so
the codebase stays easy to change as UI/UX features are added.

## Layers

```
Views (XAML)  ──uses──▶  ViewModels  ──uses──▶  Services  ──uses──▶  Data (EF Core / SQLite)
   │                         │                     │
Controls (reusable       Models (entities + enums, shared by every layer)
 UserControls)
```

* **Views** — XAML + trivial code-behind only (`InitializeComponent`). No business logic.
* **ViewModels** — `CommunityToolkit.Mvvm` (`[ObservableProperty]`, `[RelayCommand]`).
  One screen = one ViewModel. Big screens compose **child ViewModels** exposed as
  properties (see `SettingsViewModel.DataManagement`) instead of growing.
* **Services** — all real work: database access, files, backup, export. Every service has
  an interface and is registered in `App.xaml.cs`. Services never touch WPF types
  (except `DialogService`, which is the one deliberate UI seam).
* **Data** — `AppDbContext`, one `IEntityTypeConfiguration` per entity, EF migrations.
  Access it only through a service, never from a ViewModel.
* **Models** — POCO entities and enums. No behaviour.

## Navigation

`INavigationService` is ViewModel-first. `App.xaml` maps each ViewModel type to its View
with a `DataTemplate`. Use `NavigateTo<T>()` for top-level pages (clears history) and
`NavigateToDetail<T>()` + `GoBack()` for drill-down pages.

To add a page:
1. `FooViewModel : ViewModelBase` (override `LoadAsync` for data).
2. `FooView.xaml` (UserControl).
3. `<DataTemplate DataType="{x:Type vm:FooViewModel}"><views:FooView/></DataTemplate>` in `App.xaml`.
4. Register `services.AddTransient<FooViewModel>()`.
5. Add a `NavItem` in `ShellViewModel` if it belongs in the sidebar.

## Reusable UI components (`Controls/`)

Prefer a small `UserControl` with `DependencyProperty` inputs over copy-pasting XAML.
Current components: `MediaCard`, `StatusBadge`, `RatingBadge`, `EmptyState`.
Rule of thumb: if a visual pattern appears on two screens, promote it to `Controls/`.

## Theming

All colours live in `Resources/Themes/DarkTheme.xaml` as `Color.*` / `Brush.*` keys.
Never hard-code a colour in a View or Control — reference a brush key. A future light
theme is just a parallel dictionary with the same keys.

## Strings & localization

User-facing enum labels go through `Utilities/EnumDisplay`. Keep other user-facing text
out of C#; when localization work starts, these become resource lookups.

## Offline / network rule

The only network code lives under `Metadata/`. It is called **only** from an explicit
button (`Search online`, `Refresh metadata`), only when `network.onlineSearchEnabled` is on,
and never on startup, on a timer, or in the background. Poster/backdrop downloads need the
separate `network.posterDownloadEnabled` switch too. New providers implement
`IMetadataProvider` and register in `App.xaml.cs`; nothing else changes.

API keys go through `ISecureSettingsService` (DPAPI-encrypted). Never log a key or a full URL
that contains one.

## Data locations

| What | Where | Notes |
|---|---|---|
| Data-directory pointer | `anivault.config.json` next to the exe (portable), else `%LOCALAPPDATA%\AniVault\bootstrap.json` | only a path, no secrets |
| Database | `<data>/Database/AniVault.db` | EF migrations on startup |
| Artwork | `<data>/{Anime,Movies,TV}/<mediaId>/poster.*` and `backdrop.*` | DB stores the **relative** path; managed only by `ArtworkService` |
| Poster thumbnails | `<data>/Cache/thumbnails/poster-<mediaId>.jpg` | derived, safe to delete, not backed up |
| Logs | `<data>/Logs/app.log` | rotates at ~2 MB |
| Backups | `<data>/Backups/*.zip` (default target) | user may pick any folder |

## Backup format

`AniVault_Backup_<timestamp>.zip` containing `backup-manifest.json` + the `Database/`,
`Anime/`, `Movies/`, `TV/` folders. Restore replaces those folders and requires a restart.
Logs and cache are not backed up.
