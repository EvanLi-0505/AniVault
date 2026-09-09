using AniVault.Models;
using AniVault.Services.Artwork;
using AniVault.ViewModels;

namespace AniVault.Services;

/// <summary>
/// Builds <see cref="MediaCardViewModel"/> instances and kicks off their (async, non-blocking)
/// artwork load. Keeps card creation identical across the Home, library and season pages.
/// </summary>
public interface IMediaCardFactory
{
    MediaCardViewModel Create(Media media);
}

public sealed class MediaCardFactory : IMediaCardFactory
{
    private readonly IArtworkService _artwork;

    public MediaCardFactory(IArtworkService artwork)
    {
        _artwork = artwork;
    }

    public MediaCardViewModel Create(Media media)
    {
        var card = new MediaCardViewModel(media, _artwork);
        _ = card.LoadArtworkAsync();
        return card;
    }
}
