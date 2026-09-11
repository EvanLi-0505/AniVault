# Implementation status

## Done — Milestone 1 (Phase 1 + Phase 2)

### Phase 1 — Project foundation
- WPF app targeting `net10.0-windows`, x64.
- Dependency injection via `Microsoft.Extensions.DependencyInjection` (`App.xaml.cs`).
- MVVM with `CommunityToolkit.Mvvm`.
- Dark theme + reusable control styles; ViewModel-first navigation with a back stack.
- Lightweight file logging to `<data>/Logs/app.log`; global exception handlers.
- Offline first-run setup (choose data folder → create layout → create DB → start).
- Portable data-directory pointer: `anivault.config.json` next to the exe, `%LOCALAPPDATA%` fallback.

### Phase 2 — SQLite + EF Core
- `AppDbContext` with `Media`, `Episode`, `Tag`, `MediaTag`, `MediaExternalId`, `AppSetting`.
- Enums stored as integers; indexes + unique constraints; cascade deletes.
- `InitialCreate` migration applied automatically on startup (idempotent, non-destructive).

## Done — Milestone 2 (Phase 3 + selected Phase 5 / Phase 12 + new requests)

### Local media management (Phase 3)
- Add / edit / delete media, fully offline, via the modal editor.
- Anime / Movies / TV Series library pages (shared `LibraryViewModel`) with local text search.
- **Media detail page**: full metadata, quick status / rating / favorite / liked changes,
  official-website launch (opens the normal browser), edit & delete.

### Episodes (Phase 5, partial)
- Detail page episode checklist; "Create episode list" from the episode count.
- Watched count ("8 / 28 episodes watched"); non-automatic "All episodes watched → Mark as Completed?" prompt.
- `MediaService` episode mutators with watched timestamps.

### Backup & restore (Phase 12) — *requested*
- `BackupService`: ZIP of database + artwork + `backup-manifest.json`.
- Settings → "Back up now…" (choose any folder), "Restore from backup…" (validates, warns,
  replaces the library, prompts a restart), "Open backup folder".
- WAL checkpoint before backup; pooled connections released before restore.

### Markdown export — *requested*
- `LibraryExportService` + `MarkdownLibraryWriter` (pluggable writer).
- Settings → "Export library to Markdown…": a small summary table, then a section per media
  type (`## Anime (n)`), split into watch-status groups (`### ✓ Completed (n)`), **one compact
  line per title** — name, broadcast year/season, ★personal rating, episode progress, ❤/👍
  marks, tags — with the original title and any notes on indented follow-up lines. Items are
  ordered by personal rating within each group. Headings/labels are localized (en / 中文).
- The logs section of Settings also has **"Clear logs"** (deletes `app.log` / `app.log.1`
  under the write lock; the app keeps logging afterwards).

### Packaging (Phase 13) — *requested*
- App icon generated from code (`build/make-icon.ps1` → committed `AniVault.ico`).
- `build/publish.ps1`: self-contained, single-file, compressed **win-x64** publish →
  `dist/AniVault-<version>-win-x64-portable.zip` (one `AniVault.exe`, no .NET needed).
- `build/make-installer.ps1` + `build/installer/AniVault.iss`: optional per-user installer
  (`dist/AniVault-<version>-Setup.exe`) — no admin prompt, Start-menu shortcut, clean uninstall,
  never touches the user's data folder.
- `dist/SHA256SUMS.txt`; `dist/` git-ignored (binaries → GitHub Releases).
- `.github/workflows/ci.yml` (build+test) and `release.yml` (tag `v*` → build + attach to Release).
- Verified: portable exe and installed exe both launch offline, migrate, show UI; install→run→uninstall cycle clean.

### Library browsing polish — *requested*
- The filter dropdowns (Status / Year / Season) now use `FilterChoice<T>` wrapper items instead
  of a bare `null` entry, so the "All" / "(none)" option can be re-selected (a WPF `Selector`
  cannot re-select a null item once a real value was chosen — you were stuck until "Reset").
- Every library / status / Favorites / Liked / Search / tag page paginates at **60 cards**;
  the tag filter paginates at **30**. `LibraryViewModel` keeps the full result list and only
  builds card VMs for the visible page.
- The **My Rating** page shows a one-paragraph summary of the bundled 10-point rating rubric
  (`Resources/rating-guide.md`, embedded) with an "Open the full rubric" button →
  `RatingGuideWindow` (a resizable, scrollable window; `IRatingGuideService`, `Utilities/MarkdownFlow`
  renders the Markdown to a `FlowDocument`).
- **Rating questionnaire**: a "📋 Questionnaire" button next to the rating box on the detail
  page opens `RatingCalculatorWindow` — pick five facet scores 0–10 (ComboBoxes), tick two
  bonuses; final = min(sum ÷ 5 + bonus, 10.0); "Assign rating" writes it back. The window also
  links to the full rubric. `IRatingCalculatorService` launcher, `RatingCalculatorViewModel`.

### Resilience — *requested*
- **Single instance.** `SingleInstanceGuard` (named mutex + event): a second launch signals the
  running instance to come to the front, then exits. No more stacked processes fighting over the
  SQLite file. (Skipped under `ANIVAULT_SMOKE`.)
- The dispatcher exception handler now rate-limits its "kept running" dialog (once / 5 s) and,
  after 5 unhandled UI exceptions within 2 minutes, offers a clean **restart**.

### Tags & search (Phase 6) — *requested*
- `TagService`: create / rename / delete / delete-unused, case-insensitive de-dup, assign to media.
- Tag editor component (`TagPickerViewModel`) in the media editor; clickable tag chips on the detail page.
- **Tags page**: every tag with usage count; rename, delete, remove unused, open as a filtered view.
- `MediaQueryService` + `MediaFilter` / `MediaSortOption`: one place translates combined filters
  (type, text incl. tag names, status, year, season, **month**, favorite, liked, min rating, tags
  any/all) + sort (title, my rating, broadcast/added/updated/completed date, episode count, asc/desc).
- Shared `FilterPanel` control on every browse surface. The Month box is an editable combo:
  quarterly-premiere presets (1/4/7/10) plus free-typed text ("3", "12月", …) — any 1-12 value
  works, not just the four preset months. Backed by `Media.AirMonth` (`int?`, migration
  `AddAirMonth`), settable in the editor and set from a provider's `StartDate` on import/refresh
  (provider-owned, like `AirYear`/`AirSeason`).
- Sidebar wired up: each **status**, **Favorites**, **Liked**, **My Rating** and **Search** are now
  real filtered library views (were placeholders).

### Online metadata search — discoverability
- The library header "🌐 Search online" button is now always shown on the Anime / Movies /
  TV Series pages (it was hidden entirely until `network.onlineSearchEnabled` was on, so users
  couldn't find it). If online search is off, pressing it asks to turn it on first (one Yes),
  then continues — still no network until the user explicitly enables it *and* runs a search.

### Metadata providers (Phase 10) — *done*
- `Metadata/IMetadataProvider` + isolated providers, **user-selectable in Settings**:
  - **Bangumi** (default) — anime & live-action, Chinese titles, no key; may need a VPN in mainland China.
  - **AniList** — anime, no key; often works without a VPN but the API is sometimes down.
  - **Jikan (MyAnimeList)** — anime, no key; MAL data, English/Japanese titles, usually no VPN needed.
  - **Kitsu** — anime, no key; English/romaji titles (uses JSON:API).
  - **TMDB** — movies & TV; needs a free API key (entered in Settings, stored DPAPI-encrypted).
  - **Custom** — one user-defined REST provider. "Edit custom provider…" in Settings takes a
    search URL with `{query}`, an optional details URL with `{id}`, an optional API-key header
    (key stored DPAPI-encrypted), and dotted JSON field paths (`data.results[0].title`).
    Config JSON lives in `SettingKeys.CustomProvider`; `CustomProviderStore` loads it at startup.
    `JsonPath` is the tiny path selector; `IMetadataProvider.IsConfigured` (default `true`)
    keeps an un-set-up custom provider out of the "ready" set.
- `MetadataService`: picks the active provider, isolates API keys, and **refuses every network
  call unless "online metadata search" is enabled** in Settings.
- `ProviderHttp`: one configured `HttpClient` (20s timeout, cancellation), friendly errors for
  offline / rate-limit / refused.
- Online workflow (`OnlineSearchWindow`): "🌐 Search online" on each library → pick provider →
  query → results → preview → **Add to Library** (with duplicate detection: open existing / add
  anyway / cancel). Optional poster+backdrop download, gated by the separate Settings switch.
- Detail page **"↻ Refresh metadata"**: re-fetches provider-owned fields only; rating, tags,
  notes, favorite/liked, status and watched episodes are never touched (spec §60).
- `SecureSettingsService` (DPAPI) for API keys; `ImageDownloadService` (size-capped, image-only).
- **No automatic or background network access anywhere** — verified: a full page tour on launch
  makes zero HTTP requests.
- Season derivation refined: Winter = Dec–Feb, Spring = Mar–May, Summer = Jun–Aug, Fall = Sep–Nov
  (so a late-September premiere is Fall); December rolls into the next year's Winter.

### Startup robustness — *fixed*
- First run on a machine with no config used to crash ("could not start"): resolving
  `FirstRunViewModel` forced the EF `DbContextOptions` to build, which read the not-yet-set
  data path. Replaced `AddDbContextFactory` with `RuntimeDbContextFactory` that reads the
  path lazily on the first `CreateDbContext()`.
- Fatal startup errors now write `AniVault-startup-error.txt` (next to the exe, else `%TEMP%`)
  so a pre-data-folder crash is diagnosable.
- The smoke test now performs a **genuine first run** (no pre-seeded config).

### Themes — *done*
- `Resources/Themes/{Dark,Light}Theme.xaml` — same key set, swapped at runtime by `ThemeService`.
- Settings → Appearance → Theme (Dark / Light); choice persisted, applied on startup.
- Every View/Control uses `DynamicResource Brush.*`, so the swap is instant with no restart.
- Retemplated `ComboBox` / `ComboBoxItem` and `DatePickerTextBox` / `Calendar` / `CalendarItem` /
  `CalendarDayButton` / `CalendarButton` (`Resources/Styles/Controls.xaml`): themed popup /
  drop-down (`Brush.Surface`), readable text, hover / selected states — the stock templates
  rendered near-invisible text (and a hard-coded dark-grey day-of-week header) on the dark surface.
- Fixed a first-run crash ("Something went wrong, but AniVault will keep running"): the shell's
  `PageHost` page-transition used a `Binding.TargetUpdated` `ControlTemplate` trigger whose
  `Storyboard.TargetName` failed to resolve during the first layout pass
  (`'P' name cannot be found in the name scope`). The fade/lift is now driven from
  `MainWindow.OnPageChanged` code-behind (animating the `ContentControl` by reference).
- The smoke test now also load-and-closes the Add-media editor, Online-search, First-run and a
  bare themed `Calendar` (in all three display modes) — the page tour never opened these.

### Localization (English / 中文) — *done*
- `LocalizationService` + `Resources/Strings/{en,zh}.json` (embedded, ~305 keys each, identical
  key sets, `{0}`-style placeholders). `en` is the fallback for any missing key.
- XAML uses the `{loc:Loc Key}` markup extension (binds to an `INotifyPropertyChanged` indexer);
  C# uses `_loc.Text(key)` / `_loc.Format(key, args)` or the non-DI `LocalizationService.Instance`.
- Settings → Appearance → Language (English / 中文); persisted (`language` setting), applied on
  startup. Switching re-raises the string indexer and fires `LanguageChanged`, so open pages and
  the sidebar update live with no restart; transient VMs rebuild on navigation.
- `EnumDisplay` resolves `WatchStatus` / `MediaType` / `AnimeSeason` / `MediaSortField` labels
  through the string table.
- Smoke test re-visits every page in Chinese, then restores the previous language.

### UI polish (Phase 9) — *done*
- Page transitions: content fades + rises when navigating (`PageHost` style).
- Slim dark scrollbars; sidebar selected item gets a left accent bar.
- Thin indeterminate busy bar at the top of the content area, bound to the page's `IsBusy`.
- `MediaCard` lifts slightly on hover; `RatingStars` control (five stars + exact number).
- Detail page: status badge + star rating under the title; Favorite/Liked are stateful toggle
  buttons; **Esc** goes back.
- Home page rebuilt with horizontally-scrolling rows (`MediaRow` control): Continue watching,
  Recently added, Highest rated; uses the shared `EmptyState`.
- Multi-collection `Include` queries switched to split queries (removes the EF perf warning).

### Artwork (Phase 8) — *done*
- `ArtworkService` owns all local image files: import poster/backdrop into `<data>/<Type>/<id>/`,
  swap file extensions cleanly, delete on clear or media-delete.
- On-disk poster **thumbnails** in `<data>/Cache/thumbnails/poster-<id>.jpg` (regenerated if stale);
  grid cards load the thumbnail, the detail page loads the full poster (500px) and backdrop (1280px).
- `ImageLoading` gained a bounded in-memory decode cache (160 entries, LRU) so re-visiting a page
  doesn't re-read files; decoding happens on a background thread.
- Editor: side-by-side poster + backdrop pickers with live preview.
- Detail page: backdrop shown as a gradient-scrimmed banner when present.
- Missing / corrupt / unsupported images fail gracefully ("No poster", warning on bad type).
- `MediaCardFactory` centralises card creation across Home / library / seasons pages.

### Anime seasons (Phase 7) — *done*
- `SeasonsViewModel` + `SeasonsView`: left year list, four season tabs with per-year counts,
  poster grid for the selected `2026 Summer`-style broadcast season.
- `SeasonHelper` maps months → Winter/Spring/Summer/Fall; the page opens on the current season.
- `MediaQueryService.GetAnimeSeasonBucketsAsync` powers the year/season counts.
- "📅 Browse by season" button on the Anime library header.

### Componentization
- `Controls/`: `MediaCard`, `MediaRow`, `StatusBadge`, `RatingBadge`, `RatingStars`, `EmptyState`, `TagChip`, `FilterPanel`.
- `Utilities/ImageLoading` shared by card + detail poster loading.
- Child ViewModels (`BackupExportViewModel`, `FilterPanelViewModel`, `TagPickerViewModel`).
- `LibraryViewModel` is preset-driven (`LibraryPreset`) — one class backs libraries, status views,
  Favorites, Liked, by-rating, Search and tag views.
- `ANIVAULT_SMOKE=1` visits every page on launch then exits (CI smoke test); WPF binding errors
  are forwarded to the log.
- Conventions in `docs/ARCHITECTURE.md`.

## Verified
- `dotnet build AniVault.slnx -c Release` — 0 warnings, 0 errors.
- `dotnet test AniVault.slnx` — 98 passing (schema/migrations, cascade delete, unique
  constraints, media CRUD, episode sync/watched/rating clamp/completed-stamp, backup round-trip,
  Markdown export, combined query filters + sort incl. month, filter-panel choice round-trip +
  month free-text parsing + tag paging, rating-calculator maths, single-instance guard, tag
  service, season mapping + buckets,
  artwork import/thumbnail/clear/delete, AniList/Bangumi/Jikan/Kitsu JSON→DTO mapping,
  `JsonPath` selector, custom-provider search/details/api-key + config round-trip,
  metadata import + duplicate detection + refresh-preserves-personal-data, online-search gate,
  encrypted API keys, theme persistence).
- Navigation smoke test: all 16 sidebar pages + a media detail page + a runtime theme swap +
  a full second page tour in Chinese + load-and-close of the editor / online-search / first-run /
  custom-provider / rating-questionnaire / rating-rubric windows and a themed `Calendar`, with
  zero exceptions, zero binding errors, and **zero network requests**.
- Manually verified: a second `AniVault.exe` launch exits in ~1 s and the first stays running.
- Manually verified: a data folder on the previous schema (`InitialCreate` only) upgrades
  cleanly on next launch — only `AddAirMonth` applies, the pre-existing row survives with
  `AirMonth = NULL`, no data loss.
- Published single-file exe + installed exe: launch offline, migrate, show the dark UI.

## Not implemented yet
- More built-in providers are drop-in (`IMetadataProvider` + one DI line + an `ExternalSource` value).
- The custom provider supports exactly **one** configuration and a fixed set of fields; it has no
  "test" button and no non-JSON response support.
- Localization covers English + 中文; adding a third language is a new `Resources/Strings/<code>.json`
  plus an `AppLanguage` enum value.
- A few OS file-dialog filter captions ("Images (*.jpg…)", "Markdown (*.md)") are still English-only.
- Editing a media item's episode count later does not auto-resync the episode list (use the detail-page button).
- Live network tests are not in the automated suite (provider mapping is covered with canned responses).
