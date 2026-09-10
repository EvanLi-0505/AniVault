# CLAUDE.md — guide for AI agents working on AniVault

AniVault is an **offline-first personal anime / movie / TV library** for Windows.
Native WPF desktop app. The local SQLite database and local artwork files are the
permanent source of truth.

## Golden rules (do not break these)

1. **.NET 10 · WPF · MVVM (CommunityToolkit.Mvvm) · EF Core · SQLite.** No WebView/WebView2,
   Electron, Node, Python, ASP.NET, or any local server. Native XAML only.
2. **Offline-first.** The app must fully work with no internet. The *only* network code is
   under `src/AniVault/Metadata/`, it runs *only* from an explicit button press, only when
   `network.onlineSearchEnabled` is on, and **never** on startup / on a timer / in the
   background. No telemetry, no auto-update, no analytics.
3. **Personal data is sacred.** Rating, tags, notes, favorite/liked, watch status and
   watched-episode state must never be overwritten by a metadata provider. "Refresh
   metadata" touches provider-owned fields only.
4. **Portable.** User data lives in a user-chosen folder, never beside the exe. Paths in the
   DB are relative to that folder. Don't embed the DB or artwork in the exe.
5. Keep dependencies minimal; prefer built-in .NET/WPF. Keep the build at **0 warnings**.

## Layout

```
src/AniVault/          the app (net10.0-windows)
  Models/  Data/  Services/  ViewModels/  Views/  Controls/  Metadata/  Resources/  Utilities/
tests/AniVault.Tests/  xUnit — business logic only, no WPF-visual tests
build/                 publish.ps1 (portable zip) · make-installer.ps1 (Inno Setup) · make-icon.ps1
dist/                  build artifacts (git-ignored; go to GitHub Releases)
docs/ARCHITECTURE.md   layer contract + conventions — READ THIS before adding a page/service
docs/STATUS.md         what's implemented, verified, and outstanding
```

## Commands

```bash
dotnet build AniVault.slnx -c Release        # must stay 0 warnings / 0 errors
dotnet test  AniVault.slnx                    # ~60 tests, run serially
dotnet run --project src/AniVault             # first run asks for a data folder

# packaging (PowerShell)
pwsh build/publish.ps1
pwsh build/make-installer.ps1

# smoke test: visits every page + a detail page + toggles theme, then exits
ANIVAULT_SMOKE=1 dotnet run --project src/AniVault
#   -> data folder from anivault.config.json next to the exe; check <data>/Logs/app.log
#   -> "Smoke test complete" with no "XAML binding" lines = healthy
```

EF migrations: `dotnet dotnet-ef migrations add <Name> --project src/AniVault`.

## Conventions (short version — full detail in docs/ARCHITECTURE.md)

- ViewModel-first navigation (`INavigationService`); `App.xaml` maps VM type → View.
- Every service has an interface and is registered in `App.xaml.cs`.
- Services never reference WPF types (exceptions: `DialogService`, `*Service` launchers in
  the composition root that open a Window).
- Colours: only `{DynamicResource Brush.*}` from `Resources/Themes/*Theme.xaml`. Never hard-code
  a hex colour in a View/Control (theme switching relies on this).
- User-facing strings: **never hard-code them**. XAML uses `{loc:Loc Namespace.Key}`; C# uses an
  injected `ILocalizationService` (`_loc.Text` / `_loc.Format`) or `LocalizationService.Instance`.
  Every key must exist in **both** `Resources/Strings/en.json` and `zh.json` (identical key sets).
  Enum labels go through `Utilities/EnumDisplay`. See `docs/ARCHITECTURE.md` → "Strings & localization".
- Reusable visual pattern used on 2+ screens → promote to a `Controls/` UserControl with
  DependencyProperty inputs.

## Current status

Phases 1–10 of the original spec are done (foundation, DB, CRUD, status/rating/fav/like,
episodes, tags + combined search/filter/sort, anime seasons, artwork + thumbnails, UI polish,
metadata providers Bangumi/AniList/Jikan/Kitsu/TMDB + one user-defined REST provider), plus
backup/restore, compact per-title Markdown export, paginated library + tag filters, a rating
questionnaire (`Resources/rating-guide.md` rubric → `RatingCalculatorWindow` / `RatingGuideWindow`),
single-instance enforcement (`SingleInstanceGuard`), portable packaging + installer, light/dark
themes, and full English / 中文 localization (runtime toggle in Settings → Appearance). See
`docs/STATUS.md` for the authoritative list and what remains.
