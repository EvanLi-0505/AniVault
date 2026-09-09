using System.Threading.Tasks;
using System.Windows.Media;
using AniVault.Models;
using AniVault.Services.Artwork;
using AniVault.Utilities;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AniVault.ViewModels;

/// <summary>
/// Display wrapper for a <see cref="Media"/> shown as a poster card in a grid.
/// Loads a small cached thumbnail (not the full poster) and formats the metadata badges.
/// </summary>
public sealed partial class MediaCardViewModel : ObservableObject
{
    private readonly IArtworkService _artwork;

    [ObservableProperty]
    private ImageSource? _poster;

    public MediaCardViewModel(Media media, IArtworkService artwork)
    {
        Media = media;
        _artwork = artwork;
    }

    public Media Media { get; }

    public int Id => Media.Id;

    public string Title => Media.Title;

    public bool IsFavorite => Media.IsFavorite;

    public bool IsLiked => Media.IsLiked;

    public MediaType MediaType => Media.MediaType;

    public WatchStatus Status => Media.Status;

    public string StatusLabel => EnumDisplay.Label(Media.Status);

    public bool HasRating => Media.MyRating is > 0;

    public double? RatingValue => Media.MyRating;

    public string RatingText => Media.MyRating is { } r ? $"★ {r:0.0}" : string.Empty;

    public string Subtitle
    {
        get
        {
            if (Media.MediaType == MediaType.Anime && Media.AirYear is { } year)
            {
                return Media.AirSeason is { } season
                    ? $"{year} · {EnumDisplay.Label(season)}"
                    : year.ToString();
            }

            return Media.AirYear is { } y ? y.ToString() : EnumDisplay.Label(Media.MediaType);
        }
    }

    public bool HasPoster => !string.IsNullOrWhiteSpace(Media.PosterPath);

    /// <summary>Loads the poster thumbnail off the UI thread. Safe when there is no poster.</summary>
    public async Task LoadArtworkAsync()
    {
        var thumbnailPath = await _artwork.GetPosterThumbnailAsync(Media);
        Poster = await ImageLoading.LoadAsync(thumbnailPath, decodePixelWidth: 360);
    }
}
