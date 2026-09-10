namespace AniVault.Models;

/// <summary>
/// The kind of media a <see cref="Media"/> record represents.
/// One shared table is used for all media; this discriminator keeps the UI
/// libraries (Anime / Movies / TV Series) separate while the storage stays simple.
/// Never compare media kinds using raw strings — always use this enum.
/// </summary>
public enum MediaType
{
    Anime = 0,
    Movie = 1,
    TvSeries = 2,
}

/// <summary>
/// The user's personal watch status for a media item.
/// User-facing labels live in the UI layer (see EnumDisplay / string resources);
/// only these stable integer values are stored in the database.
/// </summary>
public enum WatchStatus
{
    Planned = 0,
    Watching = 1,
    Completed = 2,
    OnHold = 3,
    Dropped = 4,
}

/// <summary>
/// The original broadcast season of an anime (not the month the user watched it).
/// Combined with <see cref="Media.AirYear"/> to power the season browser, e.g. "2026 Summer".
/// </summary>
public enum AnimeSeason
{
    Winter = 0,
    Spring = 1,
    Summer = 2,
    Fall = 3,
}

/// <summary>
/// Known external metadata sources. Used by <see cref="MediaExternalId"/> to prevent
/// duplicate imports and to allow a manual metadata refresh later.
/// The metadata provider system itself is not implemented in this milestone.
/// </summary>
public enum ExternalSource
{
    Bangumi = 0,
    AniList = 1,
    Tmdb = 2,
    Jikan = 3,
    Kitsu = 4,

    /// <summary>A single user-defined REST provider configured in Settings.</summary>
    Custom = 5,
}
