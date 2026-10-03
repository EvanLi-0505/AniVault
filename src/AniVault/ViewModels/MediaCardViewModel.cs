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

    /// <summary>
    /// True from construction until the thumbnail load settles — drives the card's skeleton.
    /// Starts false for an item with no poster at all, so that one shows "No poster" straight away.
    /// </summary>
    [ObservableProperty]
    private bool _isPosterLoading;

    public MediaCardViewModel(Media media, IArtworkService artwork)
    {
        Media = media;
        _artwork = artwork;
        _isPosterLoading = !string.IsNullOrWhiteSpace(media.PosterPath);
    }

    public Media Media { get; }

    public int Id => Media.Id;

    public string Title => Media.Title;

    public bool IsFavorite => Media.IsFavorite;

    public MediaType MediaType => Media.MediaType;

    public WatchStatus Status => Media.Status;

    public double? RatingValue => Media.MyRating;

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

    /// <summary>Loads the poster thumbnail off the UI thread. Safe when there is no poster.</summary>
    public async Task LoadArtworkAsync()
    {
        try
        {
            var thumbnailPath = await _artwork.GetPosterThumbnailAsync(Media);
            Poster = await ImageLoading.LoadAsync(thumbnailPath, decodePixelWidth: 360);
        }
        finally
        {
            // After Poster, so the view's fade-in starts with the image already in place.
            IsPosterLoading = false;
        }
    }
}
