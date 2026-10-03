# Implementation status

**Shipped as 2.0.0.** Everything below this line has landed, been build/test-verified (0
warnings, 0 errors), and smoke-tested on both the dev build and the packaged portable exe /
installer. See `CLAUDE.md` for the short version and the hard constraints.

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
- **Editor category override** — *requested*: the editor's "Category" field (Anime / Movie / TV
  Series) is always editable, not just set once on creation. A manual safety net for when a
  metadata provider's own type guess is wrong — Bangumi's "real" (live-action) subject type
  covers both movies and TV dramas, and `GetDetailsAsync` has no way to know which the user
  meant, so it always falls back to TV Series; an imported movie could silently land in the
  wrong library. Switching category here re-applies the same clearing rules `Save()` already had
  (e.g. leaving Anime drops the season field), it's just now reachable from the UI instead of
  only settable once at creation.

### Episodes (Phase 5, partial)
- Detail page episode checklist; "Create episode list" from the episode count.
- Watched count ("8 / 28 episodes watched"); non-automatic "All episodes watched → Mark as Completed?" prompt.
- `MediaService` episode mutators with watched timestamps.

### Backup & restore (Phase 12) — *requested*
- `BackupService`: ZIP of database + artwork + `backup-manifest.json`.
- Settings → "Back up now…" (choose any folder), "Restore from backup…" (validates, warns,
  replaces the library, prompts a restart), "Open backup folder".
- WAL checkpoint before backup; pooled connections released before restore.
- **Backup progress bar + crash safety** — *requested*: a library with many artwork files could
  take a visible moment to back up with no feedback that anything was happening.
  `IBackupService.CreateBackupAsync` now takes an `IProgress<double>` reported as each file is
  added to the archive (weighted: manifest + db snapshot + one step per artwork file, counted up
  front so the fraction is real, not a guess); Settings shows a determinate progress bar plus a
  "Creating backup… N%" status line while it runs. Crash/kill safety was mostly already in place
  (the archive is always built at `<name>.zip.tmp` and only `File.Move`d to the real `.zip` name
  after it closes successfully, so a hard kill mid-write can only ever leave an orphaned `.tmp` —
  never a truncated file masquerading as a real backup) — added the missing piece: a new backup
  now sweeps any `AniVault_Backup_*.zip.tmp` left behind by a previous interrupted run before it
  starts, so a crash doesn't accumulate stray junk in the backups folder.

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
  (`dist/AniVault-<version>-Setup.exe`) — no admin prompt, Start-menu shortcut, never touches the user's
  data folder. (Since 1.9.1 it ships no uninstaller — see "1.9.1" below.)
- `dist/SHA256SUMS.txt`; `dist/` git-ignored (binaries → GitHub Releases).
- `.github/workflows/ci.yml` (build+test) and `release.yml` (tag `v*` → build + attach to Release).
- Verified: portable exe and installed exe both launch offline, migrate, show UI; install→run→uninstall cycle clean.

### Library browsing polish — *requested*
- The filter dropdowns (Status / Year / Season) now use `FilterChoice<T>` wrapper items instead
  of a bare `null` entry, so the "All" / "(none)" option can be re-selected (a WPF `Selector`
  cannot re-select a null item once a real value was chosen — you were stuck until "Reset").
- Every library / status / Favorites / Liked / Search / tag page paginates at **up to 35 cards**
  (a whole number of rows for the current window width — see "2.0.0" below)
  (lowered from 60 — see the render-cost investigation below); the tag filter paginates at **30**.
  `LibraryViewModel` keeps the full result list and only builds card VMs for the visible page.
- **Jump to page** — *requested*: with many items, clicking ‹/› repeatedly to reach a distant page
  got tedious. Both the library pager (`LibraryView`, shared by Anime/Movies/TV/every status and
  filtered view) and the Tags page pager now have a small numeric box next to ‹ Page X/Y › — type
  a page number and press Enter (or the Go button); out-of-range or non-numeric input is ignored
  and the box clears either way. `LibraryViewModel.GoToPageCommand` / `TagsViewModel.GoToPageCommand`.
- **"← Back" from a detail page now resumes the same library page** — *fixed*: previously
  clicking into an item on page 3 and then hitting back always landed you on page 1.
  `NavigationService.GoBack()` reuses the same `LibraryViewModel` instance it pushed before
  navigating to the detail page (rather than creating a fresh one), so `CurrentPage` was already
  preserved on the object — the bug was that `LoadAsync()` → `ReloadAsync()` unconditionally reset
  `CurrentPage = 1` on every call, including this resume. `ReloadAsync` now takes a `resetPage`
  flag: `true` (the existing behavior) when a filter/sort/search change legitimately invalidates
  the old page number, `false` when `LoadAsync()` calls it directly (covers both a brand-new page,
  where `CurrentPage` is already 1 by construction, and a `GoBack()` resume, where it now stays
  wherever the user left it — `ApplyPage()`'s own clamp still handles a page number that's become
  out of range because items were added/removed while the detail page was open).
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
  The "new tag" box was a nearly-invisible sliver (a `*`-width column inside an
  `HorizontalAlignment="Left"` Grid collapses to ~0 width — a WPF star-sizing gotcha); it's now a
  full-height 🏷-prefixed input with a visible watermark.
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

### Tags page: pagination + drag-to-reorder — *requested*
- The Tags page now paginates at **20 per page** (same ‹ / › pager pattern as the library and the
  filter panel's tag list). `TagsViewModel` keeps the full `Tags` list plus a windowed
  `VisibleTags` for the current page.
- Every row has a ⠿ drag handle; dragging it to another row (same page or a different one)
  reorders the tag and persists immediately. The order is fully user-controlled — not
  alphabetical — and new tags are always appended at the end. Backed by `Tag.SortOrder`
  (migration `AddTagSortOrder`; existing installs are backfilled in the same alphabetical order
  they already had, so upgrading doesn't visibly reshuffle anything until a tag is actually
  dragged) and `ITagService.ReorderAsync`.
- **Cross-page dragging**: hovering a dragged tag over the ‹ / › pager button for ~0.6s turns the
  page without ending the drag (OLE drag-drop still pumps window messages, so a `DispatcherTimer`
  keeps ticking during the drag), so a tag can be dragged from page 2 onto page 1 and back.
  `TagsView.xaml.cs` implements this with the standard WPF click-vs-drag threshold pattern
  (`PreviewMouseLeftButtonDown`/`PreviewMouseMove` + `DragDrop.DoDragDrop`), so the row's own
  Open/Rename/Delete buttons remain clickable.
- The same order is used everywhere tags are listed — `FilterPanelViewModel` (the library's tag
  filter chips) and `TagPickerViewModel` (the editor's tag suggestions) both read
  `ITagService.GetAllWithUsageAsync()`, which now orders by `SortOrder` — so arranging tags on the
  Tags page is immediately reflected in the library filter panel, with no separate wiring needed.
- **Drag scrolling + continuous page-flip** — *requested* (two rough edges found while actually
  dragging tags): (1) hovering a dragged tag over ‹ / › used to flip exactly one page and then
  stop — now `TagsView`'s pager-hover `DispatcherTimer` keeps ticking (an initial 650ms delay,
  then every 350ms) instead of stopping itself after the first tick, so holding position over ›
  walks all the way to the last page. (2) the mouse wheel doesn't raise normal routed events
  during an OLE drag (`DragDrop.DoDragDrop` captures input for its own modal loop — a genuine WPF
  limitation, not a bug in this app's code), so scrolling the tag list while holding a tag near
  the top/bottom edge needed its own mechanism: `TagListScrollViewer`'s `PreviewDragOver` nudges
  `ScrollToVerticalOffset` on a timer whenever the drag is within 44px of the top or bottom, the
  same "auto-scroll near the edge" pattern Explorer uses for drag-and-drop lists.

### Online metadata search — discoverability
- The library header "🌐 Search online" button is now always shown on the Anime / Movies /
  TV Series pages (it was hidden entirely until `network.onlineSearchEnabled` was on, so users
  couldn't find it). If online search is off, pressing it asks to turn it on first (one Yes),
  then continues — still no network until the user explicitly enables it *and* runs a search.

### Online search: multi-select + batch import — *requested*
- `OnlineSearchWindow`'s results list is now `SelectionMode="Extended"` (`ListBoxItem.IsSelected`
  two-way bound to a per-row `OnlineResultViewModel.IsSelected`, the same pattern already used by
  `TagFilterOption`), so Ctrl/Shift-click selects several rows without disturbing the existing
  layout — a hint line above the list explains the gesture, everything else in the window
  (search bar, provider picker, preview pane, checkboxes, footer) is unchanged.
- With 2+ rows selected, **Add to Library** relabels itself ("Add *n* to Library") and imports
  every selected result in one pass: fetch details → duplicate-check → import, one row at a time.
  A possible duplicate is **skipped silently** (prompting per-row would defeat a batch add) and
  counted; a closing summary dialog reports added / skipped / failed. The window only closes if
  at least one item was actually imported. The existing single-result flow (duplicate prompt,
  open-existing / add-anyway / cancel) is untouched when 0–1 rows are selected.
- Fixed a `ComboBox` bug surfaced while testing this: `ProviderChoice` (a record) was leaking its
  auto-generated `ToString()` ("ProviderChoice { Source = ... }") into the closed selection box —
  same class of bug as `FilterChoice<T>`, same fix (`override ToString() => DisplayName`).
- "同时下载背景图" (download backdrop) now defaults to **checked**, matching the poster-download
  checkbox, instead of always starting unchecked.
- `OnlineSearchViewModelTests`: selection-count/button-label updates, `CanExecute` with multiple
  rows selected and no loaded preview, batch import counting (imported/skipped-duplicate/failed),
  window-stays-open when nothing imports, and the `ProviderChoice.ToString()` fix.

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
- Detail page: status badge + star rating under the title; Favorite/Liked/**🏠 Show on Home** are
  stateful toggle buttons; **Esc** goes back.
- Home page rebuilt with horizontally-scrolling rows (`MediaRow` control), in this order:
  **Continue watching → Highest rated → Recently added**; uses the shared `EmptyState`.
  Each row caps at 20 cards. `Media.ShowOnHome` (personal data, default true, never touched by a
  metadata refresh) lets the user exclude one item from every row via the detail-page toggle;
  `MediaFilter.ShowOnHome` / `GetRecentlyAddedAsync` enforce it.
- `MediaRow` fixes: the inner horizontal `ScrollViewer` used to swallow the page's vertical mouse
  wheel whenever the cursor was over a row (a classic nested-`ScrollViewer` bug — scrolling would
  "randomly" stop working depending on mouse position); it now forwards every wheel tick to its
  parent instead. Its `MediaCard.OpenCommand` binding used `ElementName` from inside the row's own
  `DataTemplate`, which — unlike every other `MediaCard` host in the app — didn't reliably resolve;
  switched to the same `RelativeSource AncestorType` pattern `LibraryView`/`SeasonsView` use, so
  clicking a Home card now opens its detail page.
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
- **Compress oversized artwork (manual, opt-in only)** — *requested* (perf): investigating a
  reported ~0.5s pause on some detail pages found the real original artwork (e.g. one real
  Bangumi "large" cover was 2898×4096, over 1 MB) being re-read/re-decoded from disk unshrunk
  every time, though nothing in the app ever displays a poster wider than ~500px or a backdrop
  wider than ~1280px. An earlier pass auto-capped every import to 900px/1600px, but the user
  asked for downloaded/imported artwork to always keep its original resolution — compression must
  only ever happen when they explicitly open the tool and pick which files. `ArtworkService`
  import (`SetPosterAsync`/`SetBackdropAsync`) now **never** shrinks anything; the cap logic moved
  entirely into two explicit, opt-in operations: `FindOversizedArtworkAsync` (read-only scan for
  anything wider than the display cap) and `CompressArtworkAsync(selection)` (re-encodes exactly
  the given poster/backdrop selections, in place, only replacing a file when the result is
  verifiably smaller). Settings → Data folder → **"Compress oversized artwork"** opens
  `CompressArtworkWindow`/`CompressArtworkViewModel` — a checklist of every oversized item (title,
  poster/backdrop, current width, file size), pre-checked but freely toggleable, with Select
  all/none and a "Compress selected (N)" button; nothing on disk changes just from opening it.
- **Category-switch artwork relocation** — *fixed*: a side effect found while investigating the
  above — the editor's category override (Milestone: editor category field) changed
  `Media.MediaType` but left the poster/backdrop files sitting in the *old* type's asset folder
  (e.g. `TV/52/poster.jpg` for an item now typed `Movie`). Reads still worked (the stored path is
  used directly, not re-derived from the type), but `DeleteAllArtworkAsync` computes the folder
  *from the current type*, so those files would silently become impossible to clean up on delete.
  `IArtworkService.RelocateArtworkAsync` now moves the asset folder and rewrites the stored paths;
  `MediaEditorViewModel.Save()` calls it whenever an existing item's category actually changes.
- **"Entering 番剧 pauses but 电影/剧集 don't" — root cause found, redundant reload fixed**: the
  new render-complete log (`DispatcherPriority.Render` continuation, added while chasing this)
  gave the real numbers: `Rendered 47 card(s) for '番剧库'` consistently cost **150–270ms**
  against `~20–45ms` for a 5-card Movies/TV page — proportional to card count, confirming the
  non-virtualized `WrapPanel`'s layout/render pass genuinely is the cost, not something the
  earlier "Built N card(s)" (container-creation-only) number could see. The log also showed
  **every single library navigation rendering the page twice** — `Built`/`Rendered` appearing in
  pairs a consistent ~200–275ms apart. Root cause: `LibraryViewModel.Configure()` (and the
  tag-preset branch in `LoadAsync()`) called `Filters.EndUpdate()`, which unconditionally fires
  `FilterPanelViewModel.Changed` → `LibraryViewModel`'s subscription schedules a **debounced
  200ms** reload — but `LoadAsync()` (always called immediately after `Configure()` by
  `NavigationService.Resolve`/`SetCurrent`, confirmed for every navigation path including
  `GoBack`) already does its own full, immediate reload right after, making the debounced one
  entirely redundant — so every navigation paid the ~150–270ms render cost **twice**, roughly
  doubling the reported pause. Fixed by adding `FilterPanelViewModel.EndUpdateSilently()` (resumes
  updates without firing `Changed`, for a caller — both sites in `LibraryViewModel` qualify — that
  is about to reload explicitly right after anyway) and switching both call sites to it. Verified
  in the smoke-test log: each library page now shows exactly one
  `Loaded filter options` → `Built` → `Rendered` triple instead of two. The user confirmed this
  cut the felt pause noticeably but it was still perceptible for a still-unpaginated 47-item
  library — so `LibraryViewModel.PageSize` was lowered **60 → 35** (the user's choice, after
  seeing the measured ~20–45ms-for-5-cards vs ~150–270ms-for-47-cards numbers): bounds the
  per-page render cost for any library size, and a 47-item Anime library now pages into two (35 +
  12) instead of rendering all 47 at once. A hand-written virtualizing wrap panel (WPF ships none
  built in) remains the fuller fix if 35 cards is still noticeable, but can't be verified visually
  from here, so it isn't attempted without a clearer signal that pagination alone isn't enough.

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

### Composition-root modularization — *requested*
`App.xaml.cs`'s DI registration had grown into one 85-line flat method, and startup, the
`ANIVAULT_SMOKE` test harness, and global exception handling were all tangled together in one
520-line file. Split, pure code motion, no behaviour change:
- `Composition/ServiceCollectionExtensions.cs`: `AddCoreInfrastructure` / `AddDatabase` /
  `AddDomainServices` / `AddMetadataServices` / `AddFeatureServices` / `AddViewModels` — one
  extension method per subsystem. `BuildServiceProvider` is now ~15 lines calling each.
- `App.Smoke.cs` (partial `App`): the `ANIVAULT_SMOKE` page tour + modal-window smoke checks.
- `App.CrashHandling.cs` (partial `App`): global exception handlers, the rate-limited crash
  dialog / restart offer, single-instance activation, the binding-error listener.
- `App.xaml.cs` itself is now ~230 lines: just `OnStartup` / `StartAsync` / `ShowFirstRun` /
  `BuildServiceProvider` / `OnExit`.

The intent (the user asked for this explicitly, with a future **online-watching** feature in
mind): adding a new self-contained subsystem is now "one folder + one `Add<X>Services` method +
one line in `BuildServiceProvider`", not more lines threaded into an already-long method. See
docs/ARCHITECTURE.md → "Adding a pluggable subsystem" for the pattern and for what a future
network-touching feature would specifically need to respect (its own settings switch, its own
explicit button, never automatic — same rule `Metadata/` already follows).

### 1.9.0 — browsing polish, old-backup guarantee, dead-code sweep — *requested*
- **Poster-card hover flicker fixed.** The card lifted itself 3px on hover, which moved the
  card's own bottom edge out from under a pointer resting near a bottom corner → MouseLeave →
  drop back → MouseEnter → … `MediaCard` is now a static transparent hit target (never moves)
  wrapping an inner `CardBody` that takes the lift; the border highlight keys off the
  UserControl's `IsMouseOver` instead of the Button's.
- **Tooltips are themed.** The app-wide implicit `TextBlock` style (light text) leaked into the
  stock light-yellow tooltip → near-invisible white-on-white titles. `Controls.xaml` now has an
  implicit `ToolTip` style (`Brush.SurfaceAlt` / `Brush.TextPrimary` / `Brush.Border`, wraps at
  420px), so every tooltip follows the light/dark theme.
- **Collapsible filter panel.** A "▲ Hide filters / ▼ Show filters" button sits bottom-left of the
  panel in an always-visible footer row (it is never hidden itself). Collapsing only hides the
  inputs — the filters stay applied, and a "Filters are still applied" hint shows next to the
  button when any are active. The choice is one app-wide setting (`ui.filterPanelCollapsed`), so
  it survives page changes and restarts.
- **Hideable episode list.** The detail page's Episodes header has a "Hide list / Show list"
  toggle; the choice is stored per title in the new `Media.EpisodeListHidden` column (migration
  `AddEpisodeListHidden`, default false). It is personal data: provider refresh never touches it,
  and toggling it does not bump `UpdatedAt` (it must not reshuffle "recently updated" sorting).
  The watched-progress label stays visible while the list is hidden.
- **Poster skeleton.** `MediaCardViewModel.IsPosterLoading` drives a pulsing placeholder on the
  poster area only (opacity animation, 30fps, stopped the moment the image lands, then a 180ms
  fade-in). A poster already in the in-memory image cache completes synchronously, so revisiting
  a page shows no skeleton at all; a title without a poster goes straight to "No poster".
- **Old backups keep working.** `BackupServiceTests` now builds a database at the 1.8.0 schema
  with raw SQL, zips it as a backup, restores it and runs the startup migration — every personal
  field, episode, tag and artwork file survives and `EpisodeListHidden` defaults to false. The
  same path was also run once against a real 1.8.0 backup (482 titles / 489 tags / 190 episodes /
  482 poster files: all present after restore + migrate). Backups are whole-database copies and
  `DatabaseInitializer` migrates on every start, so any older backup is upgraded on first launch.
- **Dead-code sweep** (cross-checked with a reference scan and an IDE0051/52/59/60 analyzer pass,
  which came back clean afterwards): removed the never-navigated `PlaceholderViewModel` /
  `PlaceholderView`; `IMediaService.GetLibraryAsync` (superseded by `MediaQueryService`; only its
  own two tests called it); the temporary `Loaded filter options` / `Built` / `Rendered` /
  `Regenerated thumbnail` timing logs left from the 1.6.x perf investigation (which also removes
  the `Dispatcher` reference from `LibraryViewModel`); five unused `MediaCardViewModel` members;
  `EmptyCollectionToVisibilityConverter`; `MediaFilter.HasAnyCondition`;
  `BackupExportViewModel.DefaultBackupFolder`; `SettingKeys.SchemaNote`; eight unreferenced string
  keys. Also fixed along the way: the API-key status line in Settings and the card's Edit/Delete
  quick buttons were hard-coded English (now localized), and the editor / online-search image
  bindings no longer log an `ImageSourceConverter` warning for a missing image — the smoke log is
  now completely warning-free.
- Redundant explicit `using System…;` lines (implicit usings are on) were deliberately left: they
  are the file convention everywhere and cost nothing at runtime.

### 1.9.1 — taskbar icon for the portable build, uninstaller-free installer — *requested*
- **Taskbar icon.** Reported: the unzipped portable exe showed a generic icon on the taskbar,
  the installed copy showed the right one — same exe. Measured on the user's machine: the
  Windows 11 taskbar does not use a window's own icon unless the process has an explicit
  AppUserModelID; it resolves the icon from the shortcut that launched the app, or else from the
  shell icon cache for the exe path (a window given AniVault's icon inside `powershell.exe` still
  showed the PowerShell icon; with an explicit id it showed AniVault's). So the installed copy
  was fine because of its shortcut, and the portable copy depended on whatever the icon cache
  held for that path. `App.TaskbarIdentity.cs` now sets the AppUserModelID `AniVault.AniVault`
  before any window exists and gives every window its icon from an assembly resource, so the
  taskbar icon no longer depends on the launch method or the icon cache. The installer's
  shortcuts carry the same id (otherwise a pinned shortcut and the running window would be two
  separate taskbar buttons). Before/after check: launched through a shortcut that deliberately
  carries a wrong (star) icon, 1.9.0 shows the star on the taskbar and 1.9.1 shows AniVault's
  icon. The user's exact generic-icon state could not be reproduced from a fresh extraction, so
  the fix is verified as a mechanism rather than against their folder.
- **Installer without an uninstaller** (the user's choice: the install folder should look like
  the ZIP). `Uninstallable=no`: no `unins000.*`, no "Installed apps" entry, no uninstall shortcut;
  the folder holds `AniVault.exe` + `README.txt` (plus the `anivault.config.json` pointer the app
  writes on first run). `README.txt` is now one shared file for ZIP and installer and explains
  removal: delete the folder and shortcuts; the data folder is never deleted automatically.
  Upgrading over a pre-1.9.1 install removes the old uninstaller files, its Start-menu shortcut
  and its registry entry (checked with a renamed test build installed silently into a scratch
  folder seeded with stand-in `unins000.*` files and a stand-in registry key).

### 1.9.2 — centred layouts — *requested*
- **Settings**: the 720px card column was pinned to the left edge; it is now centred in a wide
  window (`MaxWidth` with the default Stretch alignment).
- **Poster grids** (`LibraryView` — every library / status / favourites / search page — and
  `SeasonsView`): the `WrapPanel` was left-aligned, so whatever width the fixed-size cards could
  not fill piled up as a gap on the right. The panel is now `HorizontalAlignment="Center"` with
  symmetric card margins (`7,0,7,14`), so that gap is split evenly; a page with a single short
  row (e.g. 5 movies) is centred as a whole, and a short last row stays left-aligned under the
  full rows above it.
- **"No poster" text no longer shows through a poster that is fading in** (found in the
  verification screenshots; introduced with the 1.9.0 skeleton): it is now shown only when the
  card is not loading *and* has no image.
- Verified from `PrintWindow` screenshots of the Debug build during the smoke tour, on a copy of
  a real 482-title library at 2400x1400: Settings, Movies (5 cards), Liked (14 cards), Seasons.

### 2.0.0 — whole-row pages, no leftovers in the temp folder — *requested*
- **Pages always end on a full row.** The page size was a fixed 35, which is only a whole number
  of rows at 7 (or 5) cards per row; at any other window width the page ended in a short row
  with a gap. The page size is now the largest whole number of rows within 35 for the current
  row width — 7 per row → 35, 6 → 30, 5 → 35, 4 → 32, 3 → 33 (`LibraryLayout.PageSizeFor`); 35
  stays the upper bound, so the per-page render cost is unchanged.
  - `LibraryView` code-behind measures the card area, fixes the grid at a whole number of
    186px columns (`CardGrid.MaxWidth`) and reports the count via `LibraryViewModel.SetColumns`.
    The vertical scrollbar's width is always reserved, so the scrollbar appearing or
    disappearing can never change the column count (which would change the page, which could
    toggle the scrollbar again).
  - Resizing keeps the user's place: the page number is recomputed so the card that was first
    on screen is still on the page shown.
  - `LibraryLayout` is one shared instance, so a newly opened library page starts with the row
    width the last one measured and loads the right page size straight away — navigation still
    renders a page once, not twice (the 1.6.0 fix is preserved).
  - Verified on a copy of a real 472-anime library through UI Automation + window captures:
    6 per row → "page 1 / 16" and a full last row of 30; 4 per row → "1 / 15" and a full last
    row of 32; 7 per row → "1 / 14".
- **No leftovers in `%TEMP%`.** The single-file exe makes the .NET host unpack its native
  libraries into `%TEMP%\.net\AniVault\<build id>\` once per build and never remove them, so
  every update left the previous build's copy behind (the only thing AniVault left on the system
  drive outside its own folders; it writes nothing to the registry). On a normal start the app
  now deletes the folders of other builds in the background (`ExtractionCacheCleaner`, called
  from `App.StartAsync`): only when it is itself running from such a folder, never the folder it
  is using, and a folder another running copy has libraries loaded from is left whole (rename
  first — which fails as a unit while any file inside is open — then delete). Skipped under
  `ANIVAULT_SMOKE`, which bypasses the single-instance guard.
- **Settings shows where that folder is.** The running build's own folder (about 10 MB) is the
  one thing the app cannot delete for the user, so the Data folder card now has a "Temporary
  runtime files" section: what the folder is, that older copies are cleaned automatically, that
  it holds no library data, its path, and an "Open temp folder" button
  (`ExtractionCacheCleaner.GetCacheRoot`). With the data folder and the config path already
  listed on that card, every place AniVault leaves files is now visible from Settings.
- Final review before release: analyzer pass (unused members / values / parameters) clean, string
  tables in parity with no unreferenced or missing keys, no hard-coded colours in views.
  `ProviderHttp.SendAsync` now disposes the request it is handed.

## Verified
- `dotnet build AniVault.slnx -c Release` — 0 warnings, 0 errors.
- `dotnet test AniVault.slnx` — 157 passing (schema/migrations, cascade delete, unique
  constraints, media CRUD, editor category override + save + Anime-field clearing on switch +
  artwork relocation on category switch, `ShowOnHome` default/persist/recently-added-exclusion, episode
  sync/watched/rating clamp/completed-stamp, backup round-trip + progress reporting +
  stale-tmp cleanup + restore-and-upgrade of a previous-schema backup, filter-panel collapse
  persistence, episode-list-hidden persistence, poster-skeleton state,
  Markdown export, combined query filters + sort incl. month + show-on-home, filter-panel choice
  round-trip + month free-text parsing + tag paging + silent-vs-firing bulk update, rating-calculator maths, single-instance
  guard, tag service incl. `SortOrder` ordering + reorder + append-at-end, tags-page pagination +
  drag-reorder across pages + jump-to-page, library pagination + jump-to-page +
  page-preserved-on-filter-vs-resume, season mapping + buckets,
  artwork import always keeps original resolution + oversized-artwork scan/selective-compress +
  folder relocation +
  compress-existing-artwork pass, AniList/Bangumi/Jikan/Kitsu JSON→DTO mapping,
  `JsonPath` selector, custom-provider search/details/api-key + config round-trip,
  metadata import + duplicate detection + refresh-preserves-personal-data, online-search gate,
  encrypted API keys, theme persistence, online-search multi-select + batch import).
- Navigation smoke test: all 16 sidebar pages + a media detail page + a runtime theme swap +
  a full second page tour in Chinese + load-and-close of the editor / online-search / first-run /
  custom-provider / rating-questionnaire / rating-rubric windows and a themed `Calendar`, with
  zero exceptions, zero binding errors, and **zero network requests**.
- Manually verified: a second `AniVault.exe` launch exits in ~1 s and the first stays running.
- Manually verified: a data folder on the previous schema (`InitialCreate` only) upgrades
  cleanly on next launch — only `AddAirMonth` applies, the pre-existing row survives with
  `AirMonth = NULL`, no data loss.
- Manually verified: a data folder on the pre-`ShowOnHome` schema upgrades with the new column
  **defaulted to `1` (true)** for every existing row — an upgrade never silently drops existing
  items off the Home page. (The EF-generated migration defaulted the column to `false`, which
  would have done exactly that; caught and corrected before shipping.)
- Published single-file exe + installed exe: launch offline, migrate, show the dark UI.

## Not implemented yet
- More built-in providers are drop-in (`IMetadataProvider` + one DI line + an `ExternalSource` value).
- The custom provider supports exactly **one** configuration and a fixed set of fields; it has no
  "test" button and no non-JSON response support.
- Localization covers English + 中文; adding a third language is a new `Resources/Strings/<code>.json`
  plus an `AppLanguage` enum value.
- A few OS file-dialog filter captions ("Images (*.jpg…)", "Markdown (*.md)") are still English-only.
- The metadata providers' descriptions in Settings and their network-error messages
  (`MetadataProviderException`) are English-only too.
- Editing a media item's episode count later does not auto-resync the episode list (use the detail-page button).
- Live network tests are not in the automated suite (provider mapping is covered with canned responses).
