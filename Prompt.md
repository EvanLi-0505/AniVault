# AniVault — Offline-First Personal Anime & Media Library

## 0. Role

You are the primary software engineer for this project.

Build a production-quality Windows desktop application named **AniVault**.

The user is not an experienced C# developer, so the codebase must be:

- clean
- understandable
- maintainable
- well structured
- strongly typed
- reasonably commented
- easy for another AI coding agent to continue modifying

Do not over-engineer the project.

Prioritize reliability, simplicity, offline functionality, and maintainability over unnecessary abstractions.

Do not ask the user to implement large portions of the code manually. You should create and modify the project files yourself.

------

# 1. Product Definition

AniVault is a lightweight personal media library for Windows.

Its primary purpose is to allow a user to manage:

1. Anime
2. Movies
3. TV Series

The application should allow the user to:

- search for anime/media metadata online when explicitly requested
- save metadata locally
- optionally download posters and other artwork into a user-selected data folder
- organize media by type
- organize anime by broadcast year and season
- assign watch status
- rate media
- assign tags
- mark favorites
- mark media as liked/enjoyed
- mark individual episodes as watched/unwatched
- search and filter the local library
- use the entire library completely offline

The application is NOT a streaming application.

It does NOT play videos.

It does NOT download videos.

It does NOT provide torrent functionality.

It is a personal media catalog and tracking application.

------

# 2. Core Philosophy

The most important architectural principle is:

> LOCAL DATA IS THE SOURCE OF TRUTH.

The application must be **offline-first**.

Internet connectivity is an optional auxiliary feature used only when the user explicitly requests metadata searching or artwork downloading.

The core application must continue to work normally with no Internet connection.

The application must never require an online server.

------

# 3. HARD TECHNICAL REQUIREMENTS

Use the following technology stack:

- Language: C#
- Target Framework: .NET 10
- UI Framework: WPF
- UI markup: XAML
- Architecture: MVVM
- MVVM library: CommunityToolkit.Mvvm
- Database: SQLite
- ORM: Entity Framework Core
- Target OS: Windows 10 and Windows 11
- Target architecture: x64

Use:

```
net10.0-windows
```

for the WPF application.

Do NOT use:

- Electron
- Tauri
- WebView
- WebView2
- Chromium
- React
- Vue
- Angular
- HTML/CSS as the primary UI
- Node.js runtime
- Python runtime
- ASP.NET server
- localhost server
- MySQL
- PostgreSQL
- Redis
- cloud backend

This must be a native WPF desktop application.

------

# 4. Deployment Requirements

The final application must support portable / green deployment.

The preferred user experience is:

1. Download ZIP
2. Extract ZIP
3. Double-click `AniVault.exe`
4. Application starts

No installer should be required for the primary distribution method.

The primary release target should be:

- Windows x64
- self-contained
- single-file executable where practical

Use an appropriate .NET single-file publish configuration.

The program executable may be a single EXE, while user data must remain outside the executable.

Do NOT embed the SQLite database or user media artwork into the executable.

The release architecture should conceptually be:

```text
AniVault/
    AniVault.exe
```

while user data can live elsewhere, for example:

```text
D:\AniVaultData\
```

The application must allow the user to choose the data directory.

Do not assume that the EXE directory is writable.

------

# 5. Data Directory

The user must be able to choose a custom data directory.

Example:

```text
D:\AniVaultData\
```

Recommended structure:

```text
AniVaultData/
│
├── Database/
│   └── AniVault.db
│
├── Anime/
│   └── <media-id>/
│       ├── poster.jpg
│       └── backdrop.jpg
│
├── Movies/
│   └── <media-id>/
│       ├── poster.jpg
│       └── backdrop.jpg
│
├── TV/
│   └── <media-id>/
│       ├── poster.jpg
│       └── backdrop.jpg
│
├── Backups/
│
└── Cache/
```

The actual directory design can be improved if necessary, but keep it simple.

Paths stored in the database should preferably be relative to the configured data directory rather than absolute paths whenever practical.

This makes the library more portable.

------

# 6. First Launch

On first launch:

1. Detect whether a data directory has been configured.
2. If not, show a first-run setup screen.
3. Ask the user to select a data folder.
4. Create the necessary subdirectories.
5. Create the SQLite database.
6. Apply Entity Framework Core migrations.
7. Start the application.

Do not require an Internet connection during first launch.

The user must be able to configure the application completely offline.

------

# 7. Media Architecture

Use a shared media model internally, while presenting separate libraries in the UI.

The application must have these logical library types:

```text
Anime
Movies
TV Series
```

Prefer one SQLite database with a `MediaType` field rather than creating three independent SQLite database files.

The UI should still make these libraries appear independent:

```text
Anime
Movies
TV Series
```

This gives the user the experience of separate libraries while keeping the underlying system easier to maintain.

------

# 8. Media Types

Create a strongly typed enum such as:

```csharp
public enum MediaType
{
    Anime,
    Movie,
    TvSeries
}
```

Do not use raw strings throughout the application for media type comparisons.

------

# 9. Watch Status

The application must support exactly these primary statuses:

```text
Planned
Watching
Completed
OnHold
Dropped
```

UI labels:

```text
📥 Planned / Want to Watch
▶ Watching
✓ Completed
⏸ On Hold
✕ Dropped
```

Use a strongly typed enum.

Do not store user-facing status strings directly in the database.

------

# 10. Anime Season / Broadcast Classification

Anime must be categorized by broadcast year and season.

Use:

```text
Winter
Spring
Summer
Fall
```

and:

```text
AirYear
AirSeason
```

Optionally also store:

```text
StartDate
EndDate
```

The UI should prominently support browsing:

```text
2026
 ├── Winter
 ├── Spring
 ├── Summer
 └── Fall
```

For example:

```text
2026 Summer
```

should display anime whose broadcast season is Summer 2026.

Do NOT interpret this classification as the month the user watched the anime.

This is the original broadcast season.

------

# 11. Episode Tracking

The application does NOT need video playback progress.

Do not store:

- playback position
- playback percentage
- resume position
- timestamps from a video player
- local video files

The only episode-level tracking required is:

```text
EpisodeNumber
Title
IsWatched
WatchedAt
```

Example:

```text
Episode 01 ✓
Episode 02 ✓
Episode 03 ✓
Episode 04 ○
Episode 05 ○
```

The user should be able to mark episodes as watched/unwatched.

The application should calculate useful information such as:

```text
8 / 12 episodes watched
```

Do not automatically change the user's status without making the behavior predictable.

It is acceptable to suggest or offer:

```text
All episodes watched → Mark as Completed?
```

but avoid unexpected automatic status changes.

------

# 12. Ratings

The user must be able to rate media.

Recommended rating range:

```text
0.0 - 10.0
```

Support decimal ratings such as:

```text
8.5
9.0
9.5
```

Store the user's personal rating separately from external API ratings.

Never overwrite the user's rating with an online provider's rating.

UI example:

```text
My Rating
★★★★★★★★★☆
9.5 / 10
```

The library must support sorting by personal rating.

------

# 13. Favorites and Likes

Each media item should support:

```text
IsFavorite
IsLiked
```

The two fields must be independent.

The user can have:

```text
Favorite = true
Liked = false
```

or:

```text
Favorite = false
Liked = true
```

or both.

The UI should provide:

```text
❤️ Favorites
👍 Liked
```

These should be searchable/filterable.

------

# 14. Tags

Implement a many-to-many tag system.

Recommended entities:

```text
Media
Tag
MediaTag
```

Example tags:

```text
Action
Adventure
Comedy
Drama
Fantasy
Romance
Sci-Fi
Slice of Life
Sports
Isekai
```

A media item may have multiple tags.

A tag can belong to many media items.

Example:

```text
Frieren
 ├── Fantasy
 ├── Adventure
 └── Drama
```

The local library must support:

- filter by one tag
- filter by multiple tags
- tag-based search
- tag management
- creating custom tags
- deleting unused tags where safe

Do not restrict users to provider-supplied tags.

Users must be able to create their own tags.

------

# 15. Search

The application requires a strong local search system.

Search should be able to search at minimum:

- title
- original title
- alternative titles
- description
- tags

Search should work without Internet access.

The search box should provide fast local filtering.

Example:

```text
Search: fantasy
```

should find media with:

```text
Fantasy
```

in tags or relevant searchable metadata.

The application should also support combined filters, for example:

```text
Type = Anime
Year = 2026
Season = Summer
Status = Completed
Favorite = true
Rating >= 8.0
Tag = Fantasy
```

Filters should be combinable.

------

# 16. Sorting

Support sorting by:

- Title
- My Rating
- Broadcast Date
- Added Date
- Updated Date
- Completion Date where available
- Episode Count

Ascending and descending order should be supported.

------

# 17. Metadata Providers

Metadata retrieval must be abstracted behind an interface.

Use an architecture similar to:

```csharp
public interface IMetadataProvider
{
    string Name { get; }

    Task<IReadOnlyList<MetadataSearchResult>> SearchAsync(
        string query,
        CancellationToken cancellationToken);

    Task<MediaMetadata?> GetDetailsAsync(
        string externalId,
        CancellationToken cancellationToken);
}
```

Do not hard-code a single API directly into the ViewModel.

Potential providers:

```text
Bangumi
AniList
TMDB
```

However, providers should be modular.

Example architecture:

```text
IMetadataProvider
    │
    ├── BangumiProvider
    ├── AniListProvider
    └── TmdbProvider
```

A future provider should be addable without rewriting the entire application.

------

# 18. Provider Selection

Settings should contain:

```text
Metadata Provider

○ Bangumi
○ AniList
○ TMDB
```

The user should be able to change the active provider.

Provider-specific configuration must be isolated from the rest of the application.

If a provider requires an API key, store it securely and do not hard-code it into source code.

Do not require every provider to be enabled.

------

# 19. Metadata Scope

Metadata retrieval must remain deliberately minimal.

By default, retrieve only useful basic information such as:

```text
Title
Original Title
Alternative Titles
Description
Poster URL
Backdrop URL if available
Start Date
End Date
Broadcast Year
Broadcast Season
Episode Count
Genres / Tags
Official Website if available
External ID
```

Do NOT automatically retrieve or store:

- comments
- user reviews
- large user rating datasets
- streaming URLs
- torrent URLs
- subtitles
- video files
- character databases
- extensive staff databases
- unnecessary relationship graphs
- large social/community datasets

Keep the local database focused and lightweight.

------

# 20. VERY IMPORTANT — NO AUTOMATIC NETWORK ACCESS

The application must NOT automatically access the Internet.

Do not implement:

- telemetry
- analytics
- automatic update checking
- automatic metadata refresh
- automatic image download
- cloud synchronization
- background HTTP requests
- startup network requests
- periodic polling
- remote configuration fetching

There must be no silent network traffic.

Network access is allowed only when the user explicitly triggers a network-related action.

Examples:

```text
Search Online
Fetch Metadata
Download Poster
Refresh Metadata
```

When no such action is triggered:

```text
NO NETWORK REQUESTS
```

------

# 21. Online Search Workflow

The desired workflow is:

```text
Local Library
      ↓
User clicks "Search Online"
      ↓
Select metadata provider
      ↓
Enter search query
      ↓
HTTP request
      ↓
Display results
      ↓
User chooses result
      ↓
Preview metadata
      ↓
User clicks "Add to Library"
      ↓
Save metadata locally
```

Do not automatically add search results to the local database.

The user must explicitly confirm.

------

# 22. Poster Download Workflow

When an online result provides a poster URL:

Do NOT automatically download the poster.

Show an option such as:

```text
Poster

[Download Poster]
[Use Existing Local Image]
[Skip]
```

Only download the image after the user explicitly requests it.

The user must be able to choose the data folder.

Artwork should be saved into the configured media data folder.

Example:

```text
AniVaultData/
    Anime/
        12345/
            poster.jpg
```

The database should store a relative path such as:

```text
Anime/12345/poster.jpg
```

rather than tying the record permanently to one machine's absolute path.

------

# 23. Offline Behavior

Everything below must work without Internet:

- application startup
- library browsing
- local search
- filtering
- sorting
- adding manually
- editing media
- deleting media
- rating
- tagging
- favorites
- likes
- status changes
- episode watched state
- viewing descriptions
- viewing posters
- backups
- restoring backups
- settings

Only online metadata retrieval and explicit image downloading require Internet.

If an online function is attempted while offline, display a friendly error such as:

```text
Unable to connect to the selected metadata provider.

Your local library is still fully available offline.
```

Do not crash.

------

# 24. Main UI

Design a modern dark desktop interface inspired by the layout and visual language of modern anime library applications such as Animeko.

IMPORTANT:

Do NOT copy Animeko's source code.

Do NOT copy Animeko assets.

Do NOT reproduce the exact interface.

Only take inspiration from concepts such as:

- dark theme
- poster-focused cards
- left navigation
- large artwork
- clean spacing
- modern media-library layout
- detail pages
- compact metadata badges
- rounded cards
- visual hierarchy

Create an original UI.

------

# 25. Main Navigation

Recommended left sidebar:

```text
🏠 Home

📺 Anime
🎬 Movies
📺 TV Series

----------------

📥 Planned
▶ Watching
✓ Completed
⏸ On Hold
✕ Dropped

----------------

❤️ Favorites
👍 Liked
⭐ My Rating
🏷 Tags

----------------

📅 Seasons
🔍 Search

----------------

⚙ Settings
```

The exact icons can be changed if better native/WPF-compatible icons are available.

Avoid unnecessarily adding huge icon libraries.

------

# 26. Home Page

The Home page should provide an overview.

Possible sections:

```text
Recently Added

Continue Watching / Currently Watching

Recently Completed

Favorites

Highest Rated

Current Season
```

The Home page should remain lightweight.

Do not make it download data automatically.

Everything displayed on Home should come from the local database.

------

# 27. Anime Library Page

The Anime library should support:

- poster grid
- compact list view if practical
- search
- filters
- sorting
- status filtering
- year filtering
- season filtering
- tag filtering
- rating filtering
- favorite filtering

Include a season browser:

```text
2026

Winter
Spring
Summer
Fall
```

Selecting:

```text
2026 Summer
```

shows the anime in that broadcast season.

------

# 28. Movie Library

Movies should have their own section.

Movie-specific useful fields may include:

```text
Title
OriginalTitle
Description
ReleaseDate
Genres
Poster
Backdrop
Runtime
Country if available
MyRating
Status
Tags
Favorite
Liked
```

Do not force anime season fields onto movies in the UI.

------

# 29. TV Series Library

TV Series should have a separate library section.

Useful fields:

```text
Title
OriginalTitle
Description
StartDate
EndDate
EpisodeCount
Poster
Backdrop
Genres
MyRating
Status
Tags
Favorite
Liked
```

Episode tracking should also work for TV series.

------

# 30. Media Card

Create a visually attractive poster card.

It should show:

```text
Poster

Title

Optional:
⭐ 9.2

Status badge
```

Do not overload the card with too much information.

The poster should be the primary visual element.

Use responsive layout behavior so cards rearrange according to window size.

------

# 31. Media Detail Page

Clicking a media card should open a detailed page.

Example conceptual layout:

```text
┌───────────────────────────────────────────────┐
│                                               │
│  ┌────────────┐                               │
│  │            │      Frieren                  │
│  │   POSTER   │                               │
│  │            │      ⭐ 9.5 / 10              │
│  │            │                               │
│  └────────────┘      ✓ Completed              │
│                                               │
│                    2023 · Fall                │
│                                               │
│                    Fantasy · Adventure        │
│                    Drama                      │
│                                               │
│                    ❤️ Favorite                │
│                    👍 Liked                   │
│                                               │
│  Description                                  │
│  -----------------------------------------    │
│  ...                                          │
│                                               │
│  Episodes                                     │
│  01 ✓  02 ✓  03 ✓  04 ○ ...                  │
│                                               │
│  My Rating                                    │
│  ⭐ 9.5                                       │
│                                               │
│  Notes                                        │
│  ...                                          │
│                                               │
└───────────────────────────────────────────────┘
```

The actual design should be more polished.

------

# 32. Manual Media Creation

Users must be able to manually add media without Internet.

Provide:

```text
+ Add Media
```

The user should be able to enter:

- title
- original title
- media type
- description
- year
- season
- start date
- end date
- episode count
- status
- personal rating
- tags
- favorite
- liked
- poster
- notes

Manual creation must work completely offline.

------

# 33. Notes

Each media item should support personal notes.

Example:

```text
My Notes

"I really enjoyed the second half."
```

Notes are local-only.

Do not send notes to any online API.

------

# 34. External IDs

Allow each media item to have provider-specific external IDs.

For example:

```text
BangumiId
AniListId
TmdbId
```

Prefer a normalized external-ID structure if that is cleaner.

The purpose is to prevent duplicate imports and allow metadata refresh later.

The user's local identity for the media must remain independent of provider IDs.

------

# 35. Database Model

Create an appropriate normalized database model.

At minimum, expect entities conceptually similar to:

```text
Media
Episode
Tag
MediaTag
ExternalId / ExternalSource
AppSettings
```

Suggested `Media` properties:

```text
Id
MediaType
Title
OriginalTitle
Description
StartDate
EndDate
AirYear
AirSeason
EpisodeCount
Status
MyRating
IsFavorite
IsLiked
PosterPath
BackdropPath
Notes
CreatedAt
UpdatedAt
```

Do not blindly copy this schema if a better normalized design is appropriate.

Use Entity Framework Core migrations.

Do not use raw SQL for normal CRUD operations unless there is a strong reason.

------

# 36. Database Integrity

Implement:

- primary keys
- foreign keys
- cascade behavior where appropriate
- unique constraints where useful
- indexes for frequent searches
- indexes for status
- indexes for media type
- indexes for year/season
- sensible tag uniqueness

Prevent duplicate tags differing only by accidental casing if practical.

Prevent duplicate media records when the same provider ID is imported.

------

# 37. MVVM Architecture

Use a clean MVVM architecture.

Recommended organization:

```text
AniVault/
│
├── Models/
│
├── Data/
│
├── ViewModels/
│
├── Views/
│
├── Services/
│
├── Metadata/
│
├── Resources/
│   ├── Styles/
│   ├── Themes/
│   ├── Templates/
│   └── Icons/
│
└── Utilities/
```

Use:

```text
CommunityToolkit.Mvvm
```

for:

- ObservableObject
- ObservableProperty
- RelayCommand
- AsyncRelayCommand

Do not write unnecessary boilerplate MVVM code manually if the toolkit can safely provide it.

------

# 38. Responsibilities

### Models

Represent domain entities and enums.

### Data

Handle:

- DbContext
- EF Core configuration
- migrations
- database initialization

### ViewModels

Handle:

- view state
- commands
- user interaction
- filtering
- sorting
- navigation state

### Views

Contain XAML UI and presentation-specific logic.

Do not place business logic into code-behind unless it is genuinely view-specific.

### Services

Handle application operations such as:

- media management
- image management
- backup
- settings
- file system
- search

### Metadata

Handle:

- metadata provider abstraction
- provider implementations
- API request models
- response mapping

------

# 39. Navigation

Use a clean navigation mechanism for WPF.

Possible pages:

```text
Home
Anime
Movies
TV Series
Status
Favorites
Liked
Tags
Seasons
Search
MediaDetail
Settings
```

Avoid creating dozens of unnecessary windows.

Prefer a main shell with navigable views.

------

# 40. Image Handling

Image loading must not freeze the UI.

Posters should load asynchronously or using appropriate WPF image caching techniques.

Avoid loading huge original images into memory unnecessarily.

Use thumbnails or appropriately sized images for grid cards when beneficial.

The detailed page may load a larger image.

Handle missing images gracefully.

Example:

```text
No Poster
```

instead of throwing an exception.

------

# 41. Performance

The application should remain lightweight.

Avoid:

- unnecessary background services
- unnecessary timers
- polling
- excessive database queries
- repeatedly loading the same large images
- unnecessary dependency packages

Use asynchronous APIs for:

- database operations where appropriate
- file I/O
- metadata HTTP requests
- image downloading

Do not create a complicated caching system before there is a demonstrated need.

------

# 42. Error Handling

The application must not crash because of:

- missing poster
- corrupt image
- unavailable metadata provider
- invalid API response
- database connection problem
- offline network
- invalid user input
- inaccessible data folder

Display friendly error messages.

Detailed diagnostic information should be logged.

Do not expose stack traces to normal users.

------

# 43. Logging

Implement lightweight local logging.

Logs should be stored inside the application data folder, not necessarily beside the EXE.

Example:

```text
AniVaultData/
    Logs/
        app.log
```

Do not log:

- API keys
- sensitive user data
- personal notes
- unnecessary metadata

------

# 44. Backup and Restore

Implement local backup functionality.

At minimum provide:

```text
Backup Library
Restore Library
Open Backup Folder
```

A backup should contain:

- SQLite database
- local artwork
- relevant configuration

Prefer a ZIP-based backup format.

Example:

```text
AniVault_Backup_2026-09-09.zip
```

The restore workflow must warn the user before overwriting current data.

Backups must work completely offline.

------

# 45. Settings

Create a settings page with at least:

```text
Data Folder
Metadata Provider
Network / Online Access
Theme
Backup
Database
```

Recommended network settings:

```text
Online metadata search:
[ Enabled / Disabled ]

Poster downloading:
[ Enabled / Disabled ]
```

However, even when enabled, these features must only perform network requests after explicit user actions.

There must be no background networking.

------

# 46. Privacy

AniVault is a local-first application.

Do not implement analytics or telemetry.

Do not send user library data to external services.

Do not send:

- ratings
- notes
- tags
- watch status
- favorite state
- liked state
- personal library data

to metadata providers.

Only send the minimum search query or provider-required identifier needed to retrieve metadata.

------

# 47. Security

Never hard-code API keys into source code.

If an API key is required:

- allow the user to enter it through Settings
- store it in an appropriate local configuration mechanism
- do not display it in logs
- do not commit it to Git

Never put secrets in:

```text
appsettings.json
source code
Git history
README
```

unless they are explicitly placeholders.

------

# 48. API Abstraction Rules

The UI must never directly call:

```text
HttpClient
```

from a ViewModel.

Instead:

```text
ViewModel
    ↓
Metadata Service
    ↓
IMetadataProvider
    ↓
Selected Provider
    ↓
HTTP Client
```

This keeps network code isolated and testable.

------

# 49. HTTP Client Rules

Use a single appropriately configured HTTP client mechanism rather than repeatedly constructing unmanaged HttpClient instances.

Implement:

- sensible timeout
- cancellation support
- response status checking
- basic retry only where appropriate
- JSON validation
- graceful handling of rate limits

Do not aggressively retry failed requests.

Do not create a background polling mechanism.

------

# 50. No Streaming Functionality

Explicitly do NOT implement:

- video player
- streaming
- torrent
- download manager
- subtitle downloader
- browser
- embedded web player

This application is a media library, not a media player.

------

# 51. No WebView

This is a HARD REQUIREMENT.

Do not add:

```text
WebView2
```

or another browser component.

All application screens must be rendered natively with WPF/XAML.

External websites, if ever needed, may be opened in the user's normal browser through an explicit user action, but the application itself must not embed them.

------

# 52. Theme

Default theme:

```text
Dark
```

Visual direction:

- dark background
- slightly lighter panels
- soft rounded corners
- poster-focused cards
- subtle borders
- restrained shadows
- readable typography
- modern spacing
- high visual hierarchy

Avoid excessive animations.

Do not reproduce Animeko exactly.

Create an original visual design inspired by modern anime/media library software.

------

# 53. Responsive Window Behavior

The application should support:

- resizing
- maximization
- minimum reasonable window size

Poster grids must adapt to available width.

Avoid hard-coding layouts that only work at one resolution.

Test at:

```text
1920x1080
2560x1440
```

and a smaller laptop-oriented window size.

------

# 54. Accessibility / Usability

Use:

- readable font sizes
- sensible contrast
- keyboard-friendly controls
- tooltips for unfamiliar icons
- visible selected navigation state
- understandable empty states

Examples:

```text
No anime found.

Try changing your filters.
```

------

# 55. Empty States

Every major library must have a useful empty state.

Example:

```text
Your Anime Library is empty.

Add your first anime manually or search online for metadata.
```

Do not display a blank page.

------

# 56. Data Loading

Avoid blocking the UI thread during:

- database initialization
- loading large libraries
- image loading
- metadata search
- image download

Show lightweight progress indicators when needed.

------

# 57. Search UX

The global search box should support:

```text
Local Search
```

and an explicit action:

```text
Search Online
```

These must be clearly distinguished.

Never interpret ordinary local searching as permission to make a network request.

------

# 58. Duplicate Handling

When adding metadata from an online provider:

Try to detect potential duplicates using:

- provider external ID
- title
- media type

If a likely duplicate exists:

```text
This media may already exist in your library.

[Open Existing]
[Add Anyway]
[Cancel]
```

Do not silently create duplicates.

------

# 59. Metadata Import

When the user selects an online result:

Show a preview before importing.

Example:

```text
Title
Poster
Description
Year
Season
Episodes
Genres

[Add to Library]
[Cancel]
```

The user must explicitly confirm.

------

# 60. Metadata Refresh

Support manual metadata refresh later.

Example:

```text
Refresh Metadata
```

But never automatically refresh metadata.

When refreshing:

- preserve My Rating
- preserve Tags
- preserve Notes
- preserve Favorite
- preserve Liked
- preserve Watch Status
- preserve Episode Watched states

Remote metadata must never overwrite personal data.

------

# 61. Architecture for Personal vs Provider Data

Keep external metadata conceptually separate from personal metadata.

Provider data includes:

```text
Title
Description
Poster
Air Date
Genres
Episode Count
External IDs
```

Personal data includes:

```text
My Rating
Favorite
Liked
Status
Watched Episodes
Custom Tags
Notes
```

A metadata refresh must update provider-owned fields only.

------

# 62. Database Migrations

Use Entity Framework Core migrations.

The application should safely initialize/update the database on startup.

Do not delete user data when schema changes.

Future schema changes must be handled through migrations.

------

# 63. Testing

Create tests for important business logic.

At minimum test:

- status filtering
- tag filtering
- multiple filter combination
- rating sorting
- season filtering
- duplicate detection
- episode watched state
- backup/restore
- metadata mapping
- provider-independent logic

Do not attempt to unit test WPF visual appearance exhaustively.

Prioritize business logic.

------

# 64. Project Build Rules

The project must build cleanly.

After making substantial changes:

```text
dotnet build
```

must succeed.

Fix compiler warnings that indicate real problems.

Avoid leaving placeholder code such as:

```text
TODO implement
NotImplementedException
throw new Exception("temporary")
```

in production paths.

------

# 65. Code Quality Rules

Prefer:

- explicit models
- clear method names
- small services
- dependency injection where useful
- async APIs where appropriate
- cancellation tokens for long-running async operations
- immutable DTOs where useful
- enums for finite states
- validation near boundaries

Avoid:

- giant classes
- giant ViewModels
- global static state
- service locator patterns
- unnecessary singleton usage
- unnecessary abstraction layers
- duplicated business rules

------

# 66. Dependency Policy

Keep third-party dependencies minimal.

Before adding a NuGet package, ask:

1. Is it actually required?
2. Does WPF or .NET already provide the functionality?
3. Will it make deployment heavier?
4. Is the package mature and maintained?
5. Does it complicate self-contained publishing?

Prefer built-in .NET and WPF functionality when reasonable.

------

# 67. Localization Preparation

The first version may use English UI strings.

However, avoid scattering user-facing strings throughout C# code.

Keep UI text reasonably centralized so localization can be added later.

The architecture should allow Chinese localization in the future.

------

# 68. Folder Structure

Use a structure approximately like:

```text
AniVault.sln

src/
└── AniVault/
    ├── AniVault.csproj
    ├── App.xaml
    ├── App.xaml.cs
    ├── MainWindow.xaml
    ├── MainWindow.xaml.cs
    │
    ├── Models/
    │
    ├── Data/
    │   ├── AppDbContext.cs
    │   ├── Configurations/
    │   └── Migrations/
    │
    ├── ViewModels/
    │
    ├── Views/
    │
    ├── Services/
    │
    ├── Metadata/
    │   ├── IMetadataProvider.cs
    │   ├── Models/
    │   └── Providers/
    │
    ├── Resources/
    │   ├── Styles/
    │   ├── Themes/
    │   └── Icons/
    │
    └── Utilities/

tests/
└── AniVault.Tests/

docs/
```

You may improve this structure if a better structure is justified.

Do not create dozens of projects unless they are genuinely necessary.

Prefer a simple solution initially.

------

# 69. Recommended Development Phases

DO NOT attempt to implement the entire application blindly in one step.

Build the application incrementally.

## Phase 1 — Project Foundation

Create:

- WPF project
- .NET 10 target
- MVVM setup
- dependency injection
- base theme
- main shell
- navigation
- basic logging
- configuration foundation

Acceptance criteria:

- application starts
- navigation works
- dark UI is visible

------

## Phase 2 — SQLite and EF Core

Implement:

- DbContext
- Media
- Episode
- Tag
- MediaTag
- settings
- migrations

Acceptance criteria:

- database is created locally
- migrations work
- application can restart without losing data

------

## Phase 3 — Local Media Management

Implement:

- add
- edit
- delete
- view
- manual media creation

Everything must work offline.

------

## Phase 4 — Status / Rating / Favorite / Like

Implement:

- Planned
- Watching
- Completed
- On Hold
- Dropped
- personal rating
- favorites
- liked

------

## Phase 5 — Episodes

Implement:

- episode list
- watched/unwatched
- watched count
- manual episode management

------

## Phase 6 — Tags and Search

Implement:

- tags
- custom tags
- local search
- filters
- combined filters
- sorting

------

## Phase 7 — Anime Seasons

Implement:

- year
- Winter
- Spring
- Summer
- Fall
- season navigation

------

## Phase 8 — Artwork

Implement:

- poster selection
- local poster storage
- backdrop
- missing image handling
- image caching/loading

------

## Phase 9 — UI Polish

Implement:

- modern dark UI
- responsive card grid
- detail page
- animations only where useful
- empty states
- polished navigation

------

## Phase 10 — Metadata Provider System

Implement:

- IMetadataProvider
- MetadataService
- provider selection
- one provider first
- explicit online search
- search result preview
- import workflow

Do not implement every provider simultaneously.

------

## Phase 11 — Additional Providers

Add:

- Bangumi
- AniList
- TMDB

only as appropriate.

Each provider must remain isolated.

------

## Phase 12 — Backup / Restore

Implement:

- ZIP backup
- restore
- validation
- backup warnings

------

## Phase 13 — Packaging

Create:

- Release build
- self-contained Windows x64 build
- single-file publish configuration
- portable ZIP package

Test on a clean Windows environment if possible.

------

# 70. Important Development Rule

At every phase:

1. Build.
2. Run.
3. Verify.
4. Fix compilation/runtime issues.
5. Continue.

Do not stack many unverified changes.

------

# 71. Initial Milestone

Start with Phase 1 and Phase 2.

Do NOT implement the metadata APIs yet.

First produce a working offline local library foundation.

At the end of the first milestone, the application should be able to:

```text
Launch
↓
Choose Data Folder
↓
Create SQLite database
↓
Open main UI
↓
Navigate Anime / Movies / TV Series
↓
Manually add a media item
↓
Save it locally
↓
Close application
↓
Restart
↓
Data remains
```

There must be zero dependency on Internet connectivity for this milestone.

------

# 72. Acceptance Criteria

The final application is considered successful only when all of the following are true:

### Desktop

- Native WPF application
- no WebView
- no Electron
- no browser
- Windows 10/11 x64

### Local

- SQLite database
- user-selectable data directory
- offline library access
- local artwork

### Media

- Anime
- Movies
- TV Series

### Organization

- Year
- Winter
- Spring
- Summer
- Fall

### Status

- Planned
- Watching
- Completed
- On Hold
- Dropped

### Personal tracking

- My Rating
- Favorite
- Liked
- Notes
- Episode watched/unwatched

### Search

- local search
- tags
- combined filters
- sorting

### Network

- no automatic networking
- no telemetry
- explicit online metadata search only
- explicit poster downloading only
- switchable metadata providers

### Metadata

- basic metadata only
- provider abstraction
- provider switching
- personal metadata never overwritten

### Deployment

- portable ZIP
- self-contained
- Windows x64
- preferably single-file EXE
- no required .NET installation on target machine

### Reliability

- migrations
- backups
- restore
- logging
- graceful error handling

------

# 73. Final Instruction to Claude Code

Before writing significant code:

1. Inspect the current repository.
2. Determine whether a project already exists.
3. If this is an empty directory, initialize the solution.
4. Create a concise implementation plan.
5. Implement the first milestone.
6. Build the solution.
7. Fix all build errors.
8. Run relevant tests.
9. Summarize what was created and what remains.

Do not skip directly to API integration.

Do not introduce WebView.

Do not introduce a web server.

Do not introduce Electron.

Do not create unnecessary infrastructure.

The goal is a lightweight, maintainable, native Windows desktop application that works perfectly offline and only uses the network when the user explicitly asks it to.

The local database and local media files must remain the permanent source of truth.