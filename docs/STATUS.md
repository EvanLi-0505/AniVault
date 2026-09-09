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
- Settings → "Export library to Markdown…": summary table, then sections per media type,
  split by watch status (completed vs. watching vs. planned…), with full per-item details,
  episode progress, tags, dates, notes, external IDs.

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

### Tags & search (Phase 6) — *requested*
- `TagService`: create / rename / delete / delete-unused, case-insensitive de-dup, assign to media.
- Tag editor component (`TagPickerViewModel`) in the media editor; clickable tag chips on the detail page.
- **Tags page**: every tag with usage count; rename, delete, remove unused, open as a filtered view.
- `MediaQueryService` + `MediaFilter` / `MediaSortOption`: one place translates combined filters
  (type, text incl. tag names, status, year, season, favorite, liked, min rating, tags any/all) + sort
  (title, my rating, broadcast/added/updated/completed date, episode count, asc/desc).
- Shared `FilterPanel` control on every browse surface.
- Sidebar wired up: each **status**, **Favorites**, **Liked**, **My Rating** and **Search** are now
  real filtered library views (were placeholders).

### Metadata providers (Phase 10) — *done*
- `Metadata/IMetadataProvider` + three isolated providers, **user-selectable in Settings**:
  - **Bangumi** (default) — Chinese titles, no key; may need a VPN in mainland China.
  - **AniList** — anime, no key; often works without a VPN but the API is sometimes down.
  - **TMDB** — movies & TV; needs a free API key (entered in Settings, stored DPAPI-encrypted).
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
- `dotnet test AniVault.slnx` — 59 passing (schema/migrations, cascade delete, unique
  constraints, media CRUD, episode sync/watched/rating clamp/completed-stamp, backup round-trip,
  Markdown export, combined query filters + sort, tag service, season mapping + buckets,
  artwork import/thumbnail/clear/delete, AniList + Bangumi JSON→DTO mapping, metadata import +
  duplicate detection + refresh-preserves-personal-data, online-search gate, encrypted API keys,
  theme persistence).
- Navigation smoke test: all 16 sidebar pages + a media detail page + a runtime theme swap,
  with zero exceptions, zero binding errors, and **zero network requests**.
- Published single-file exe + installed exe: launch offline, migrate, show the dark UI.

## Not implemented yet
- Additional providers beyond the three (Phase 11) — the abstraction makes this drop-in.
- UI-string localization (e.g. Chinese) — strings are centralized enough for it but not yet extracted.
- Editing a media item's episode count later does not auto-resync the episode list (use the detail-page button).
- Live network tests are not in the automated suite (provider mapping is covered with canned responses).
