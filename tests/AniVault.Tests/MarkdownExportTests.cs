using System;
using System.Collections.Generic;
using AniVault.Models;
using AniVault.Services.Export.Writers;

namespace AniVault.Tests;

public class MarkdownExportTests
{
    private static Media Anime(string title, WatchStatus status, int episodeCount, int watched, double? rating = null)
    {
        var media = new Media
        {
            MediaType = MediaType.Anime,
            Title = title,
            Status = status,
            EpisodeCount = episodeCount,
            MyRating = rating,
            AirYear = 2023,
            AirSeason = AnimeSeason.Fall,
        };

        for (var i = 1; i <= episodeCount; i++)
        {
            media.Episodes.Add(new Episode { EpisodeNumber = i, IsWatched = i <= watched });
        }

        return media;
    }

    [Fact]
    public void Groups_By_Type_And_Status_And_Shows_Progress()
    {
        var writer = new MarkdownLibraryWriter();
        var media = new List<Media>
        {
            Anime("Frieren", WatchStatus.Completed, 28, 28, 9.5),
            Anime("Vinland Saga", WatchStatus.Watching, 24, 8),
            new() { MediaType = MediaType.Movie, Title = "Your Name", Status = WatchStatus.Completed, AirYear = 2016 },
        };

        var md = writer.Write(media);

        Assert.Contains("# AniVault Library Export", md);
        Assert.Contains("## Summary", md);
        Assert.Contains("## Anime", md);
        Assert.Contains("## Movies", md);
        Assert.Contains("### ✓ Completed (1)", md);   // one completed anime
        Assert.Contains("### ▶ Watching (1)", md);
        Assert.Contains("#### Frieren", md);
        Assert.Contains("8 / 24", md);                 // in-progress episode count
        Assert.Contains("★ 9.5 / 10", md);
    }

    [Fact]
    public void Empty_Library_Still_Produces_A_Document()
    {
        var md = new MarkdownLibraryWriter().Write(Array.Empty<Media>());

        Assert.Contains("# AniVault Library Export", md);
        Assert.Contains("0 item(s)", md);
        Assert.DoesNotContain("## Anime", md);
    }
}
