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
  an interface and is registered in `Composition/ServiceCollectionExtensions.cs`. Services
  never touch WPF types (except `DialogService` and the modal-window *launcher* services —
  `MediaEditorService`, `OnlineSearchService`, `CustomProviderService`,
  `RatingCalculatorService`, `RatingGuideService` — whose only WPF-facing job is
  `new SomeWindow { DataContext = vm }.ShowDialog()`, kept in the composition root so
  ViewModels never reference a View type directly).
* **Data** — `AppDbContext`, one `IEntityTypeConfiguration` per entity, EF migrations.
  Access it only through a service, never from a ViewModel.
* **Models** — POCO entities and enums. No behaviour.

## Adding a pluggable subsystem

`App.xaml.cs` (`BuildServiceProvider`) is intentionally thin — one call per subsystem, each
implemented as its own extension method in `Composition/ServiceCollectionExtensions.cs`
(`AddCoreInfrastructure`, `AddDatabase`, `AddDomainServices`, `AddMetadataServices`,
`AddFeatureServices`, `AddViewModels`). Adding a self-contained subsystem is meant to be:

1. A new top-level folder for its models/services (`Metadata/` is the reference shape:
   a provider interface + isolated implementations + one service that owns the gating rule).
2. One new `Add<Subsystem>Services` extension method that registers it.
3. One new line calling that method from `BuildServiceProvider`.
4. If it needs a modal UI entry point, a launcher service following the pattern in
   `AddFeatureServices` (`RatingCalculatorService` is the smallest example to copy).

This is also `App.xaml.cs`'s own shape: startup/shutdown lifecycle lives there, the DI
composition root lives in `Composition/`, the `ANIVAULT_SMOKE=1` test harness lives in
`App.Smoke.cs`, and global exception handling + single-instance activation live in
`App.CrashHandling.cs` — three concerns that used to be tangled in one file, now three
partial-class files that only share `App`'s private fields.

**If a future feature needs the network** (the most likely candidate being online watching /
streaming playback), it must follow the same rule `Metadata/` follows — nothing here is
license to add background or automatic network access:

- Its own folder (e.g. `Playback/`), never reusing `Metadata/`'s types for an unrelated purpose.
- Its own explicit settings switch (e.g. `network.onlineWatchEnabled`), off by default, checked
  before every call — see `MetadataService.EnsureAllowedAsync` for the pattern.
- Its own explicit button in the UI (e.g. "▶ Watch online" next to "🌐 Search online" on the
  detail page) — never triggered by navigation, a timer, or app startup.
- Its own `ProviderHttp`-style HTTP plumbing (or reuse `ProviderHttp` itself if the shape fits),
  never touching personal data (rating/tags/notes/status/watched state) — same rule as metadata
  refresh.

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
Current components: `MediaCard`, `MediaRow`, `StatusBadge`, `RatingBadge`, `RatingStars`,
`EmptyState`, `TagChip`, `FilterPanel`.
Rule of thumb: if a visual pattern appears on two screens, promote it to `Controls/`.

## Theming

All colours live in `Resources/Themes/{Dark,Light}Theme.xaml` as `Color.*` / `Brush.*` keys —
the two dictionaries carry the **same key set**. Never hard-code a colour in a View or Control;
reference a brush key with `{DynamicResource Brush.*}` so `ThemeService`'s runtime swap works.
Custom `ControlTemplate`s (see the `ComboBox` in `Resources/Styles/Controls.xaml`) must theme
every part — the stock templates fall back to system colours that vanish on our surfaces.

## Strings & localization

Every user-facing string lives in `Resources/Strings/{en,zh}.json` (embedded resources, one
flat `"Namespace.Key": "value"` map, `{0}` placeholders). The two files **must** have identical
key sets; `en` is the runtime fallback for a missing key.

- **XAML**: `Text="{loc:Loc Library.EmptyTitle}"` (the `LocExtension` markup extension binds to
  `LocalizationService.Instance` via an `[key]` indexer, so it re-evaluates on a language change).
  Add `xmlns:loc="clr-namespace:AniVault.Utilities"` to the file.
- **ViewModels / services**: inject `ILocalizationService` and call `_loc.Text(key)` /
  `_loc.Format(key, args)`. Composition-root helpers and value objects with no DI use the static
  `LocalizationService.Instance?.Text(...) ?? "<english fallback>"`.
- **Enum labels** go through `Utilities/EnumDisplay`, which looks up `Status.*` / `Type.*` /
  `Season.*` / `Sort.*` keys.
- Switching language persists the `language` setting, re-raises the indexer (`"Item[]"`) and
  fires `LanguageChanged`; `ShellViewModel` rebuilds the nav and the navigation service
  re-navigates so transient VMs pick up the new language. No restart.
- Adding a language: new `Resources/Strings/<code>.json` (copy `en.json`, translate), a new
  `AppLanguage` value, and a `<EmbeddedResource>` entry in the csproj.

## Offline / network rule

The only network code lives under `Metadata/`. It is called **only** from an explicit
button (`Search online`, `Refresh metadata`), only when `network.onlineSearchEnabled` is on,
and never on startup, on a timer, or in the background. Poster/backdrop downloads need the
separate `network.posterDownloadEnabled` switch too. A new built-in provider implements
`IMetadataProvider` (compose a `ProviderHttp`), adds one `AddSingleton<IMetadataProvider, …>`
line in `AddMetadataServices` (`Composition/ServiceCollectionExtensions.cs`), and an
`ExternalSource` enum value; `MetadataService` keys providers by `Source` and the Settings list
picks them up automatically. `IMetadataProvider.IsConfigured`
defaults to `true` — override it only for a provider that can be present but unusable (the
user-defined `CustomMetadataProvider`, driven by `CustomProviderConfig` + `JsonPath`).

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
