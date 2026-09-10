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
    public void Groups_By_Type_And_Status_With_Compact_Per_Title_Lines()
    {
        var writer = new MarkdownLibraryWriter();
        var media = new List<Media>
        {
            Anime("Frieren", WatchStatus.Completed, 28, 28, 9.5),
            Anime("Vinland Saga", WatchStatus.Watching, 24, 8),
            new() { MediaType = MediaType.Movie, Title = "Your Name", Status = WatchStatus.Completed, AirYear = 2016 },
        };

        var md = writer.Write(media);

        Assert.Contains("# AniVault Library", md);
        Assert.Contains("## Summary", md);
        Assert.Contains("## Anime (2)", md);
        Assert.Contains("## Movies (1)", md);
        Assert.Contains("### ✓ Completed (1)", md);   // one completed anime
        Assert.Contains("### ▶ Watching (1)", md);
        Assert.Contains("- **Frieren** — 2023 Fall  ·  ★9.5  ·  28 eps", md);
        Assert.Contains("- **Vinland Saga** — 2023 Fall  ·  8/24 eps", md);
        Assert.Contains("- **Your Name** — 2016", md);
    }

    [Fact]
    public void Sorts_By_Rating_Descending_Within_A_Status_Group()
    {
        var md = new MarkdownLibraryWriter().Write(new List<Media>
        {
            Anime("Low", WatchStatus.Completed, 12, 12, 6.0),
            Anime("High", WatchStatus.Completed, 12, 12, 9.0),
        });

        Assert.True(md.IndexOf("**High**", StringComparison.Ordinal) < md.IndexOf("**Low**", StringComparison.Ordinal));
    }

    [Fact]
    public void Emits_Original_Title_And_Notes_As_Continuation_Lines()
    {
        var media = Anime("Frieren", WatchStatus.Completed, 28, 28, 9.5);
        media.OriginalTitle = "葬送のフリーレン";
        media.Notes = "Best of the year";

        var md = new MarkdownLibraryWriter().Write(new List<Media> { media });

        Assert.Contains("  葬送のフリーレン", md);
        Assert.Contains("  Notes: Best of the year", md);
    }

    [Fact]
    public void Empty_Library_Still_Produces_A_Document()
    {
        var md = new MarkdownLibraryWriter().Write(Array.Empty<Media>());

        Assert.Contains("# AniVault Library", md);
        Assert.Contains("0 items", md);
        Assert.DoesNotContain("## Anime", md);
    }
}
