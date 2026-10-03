using System;
using System.Collections.Generic;

namespace AniVault.Models;

/// <summary>
/// A single catalogued media item (anime, movie or TV series).
///
/// Fields are grouped into two conceptual categories:
///   * Provider-owned metadata (Title, Description, air dates, EpisodeCount, poster paths, ...)
///     — these may be overwritten by a future "Refresh Metadata" action.
///   * Personal data (MyRating, IsFavorite, IsLiked, ShowOnHome, EpisodeListHidden, Status,
///     Notes, watched episodes, tags) — these must NEVER be overwritten by an online provider.
/// </summary>
public class Media
{
    public int Id { get; set; }

    /// <summary>Which UI library this item belongs to.</summary>
    public MediaType MediaType { get; set; }

    // ---- Provider-owned metadata -------------------------------------------------

    /// <summary>Primary display title. Required.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Title in the original language, if known.</summary>
    public string? OriginalTitle { get; set; }

    /// <summary>Newline-separated alternative titles. Kept as a single column to stay lightweight.</summary>
    public string? AlternativeTitles { get; set; }

    public string? Description { get; set; }

    /// <summary>Original broadcast / release start date.</summary>
    public DateOnly? StartDate { get; set; }

    /// <summary>Original broadcast / release end date.</summary>
    public DateOnly? EndDate { get; set; }

    /// <summary>Broadcast year (anime). Also usable as release year for movies/TV.</summary>
    public int? AirYear { get; set; }

    /// <summary>Original broadcast season (anime only). Not the month the user watched it.</summary>
    public AnimeSeason? AirSeason { get; set; }

    /// <summary>Broadcast / release month (1-12), if known. More precise than <see cref="AirSeason"/>.</summary>
    public int? AirMonth { get; set; }

    /// <summary>Total number of episodes, if known. Movies are typically null or 1.</summary>
    public int? EpisodeCount { get; set; }

    /// <summary>Runtime in minutes (mainly for movies).</summary>
    public int? RuntimeMinutes { get; set; }

    /// <summary>Country / region of origin, if known.</summary>
    public string? Country { get; set; }

    /// <summary>Official website URL, if provided by a source. Opened only via explicit user action.</summary>
    public string? OfficialWebsite { get; set; }

    /// <summary>Poster image path, relative to the configured data directory (e.g. "Anime/12/poster.jpg").</summary>
    public string? PosterPath { get; set; }

    /// <summary>Backdrop image path, relative to the configured data directory.</summary>
    public string? BackdropPath { get; set; }

    /// <summary>Rating supplied by an external provider (0-10). Kept separate from <see cref="MyRating"/>.</summary>
    public double? ProviderRating { get; set; }

    // ---- Personal data ---------------------------------------------------------

    public WatchStatus Status { get; set; } = WatchStatus.Planned;

    /// <summary>The user's personal rating, 0.0 - 10.0. Never overwritten by a provider.</summary>
    public double? MyRating { get; set; }

    public bool IsFavorite { get; set; }

    public bool IsLiked { get; set; }

    /// <summary>
    /// Whether this item can appear in the Home page's rows (Continue watching / Recently added /
    /// Highest rated). A personal display preference, not metadata — never touched by a provider
    /// refresh. Defaults to true so existing and newly added items show up as before.
    /// </summary>
    public bool ShowOnHome { get; set; } = true;

    /// <summary>
    /// Whether the detail page folds this item's episode checklist away (e.g. once everything is
    /// watched). Purely a personal display preference: the episodes and their watched state are
    /// untouched, and a provider refresh never changes it.
    /// </summary>
    public bool EpisodeListHidden { get; set; }

    /// <summary>Local-only personal notes. Never sent to any online API.</summary>
    public string? Notes { get; set; }

    /// <summary>UTC date the user marked this item completed, if applicable.</summary>
    public DateTime? CompletedAt { get; set; }

    // ---- Bookkeeping ----------------------------------------------------------

    /// <summary>UTC timestamp the record was created. Stored as UTC for portable, sortable ordering.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>UTC timestamp of the last change to this record.</summary>
    public DateTime UpdatedAt { get; set; }

    // ---- Navigation properties ----------------------------------------------

    public List<Episode> Episodes { get; set; } = new();

    public List<MediaTag> MediaTags { get; set; } = new();

    public List<MediaExternalId> ExternalIds { get; set; } = new();
}
