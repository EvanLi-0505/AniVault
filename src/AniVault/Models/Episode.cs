using System;

namespace AniVault.Models;

/// <summary>
/// A single episode of a <see cref="Media"/> item.
///
/// Deliberately minimal: AniVault tracks only whether an episode was watched.
/// It does NOT store playback position, percentage, resume points or video files.
/// </summary>
public class Episode
{
    public int Id { get; set; }

    public int MediaId { get; set; }

    public Media? Media { get; set; }

    /// <summary>1-based episode number within the series.</summary>
    public int EpisodeNumber { get; set; }

    public string? Title { get; set; }

    public bool IsWatched { get; set; }

    /// <summary>UTC time the user marked this episode watched. Cleared when unwatched.</summary>
    public DateTime? WatchedAt { get; set; }
}
