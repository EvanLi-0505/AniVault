using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AniVault.Models;

namespace AniVault.Metadata;

/// <summary>
/// A source of online media metadata (Bangumi, AniList, TMDB, …). Implementations are isolated:
/// each owns its own HTTP calls and response mapping. The application only ever calls these
/// methods in response to an explicit user action — never automatically.
/// </summary>
public interface IMetadataProvider
{
    ExternalSource Source { get; }

    string DisplayName { get; }

    /// <summary>One line shown in Settings, e.g. accessibility notes or "requires an API key".</summary>
    string Description { get; }

    bool RequiresApiKey { get; }

    bool SupportsMediaType(MediaType mediaType);

    Task<IReadOnlyList<MetadataSearchResult>> SearchAsync(
        string query,
        MediaType? preferredMediaType,
        CancellationToken cancellationToken);

    Task<MediaMetadata?> GetDetailsAsync(string externalId, CancellationToken cancellationToken);
}

/// <summary>Thrown by providers for a user-presentable failure (offline, rate limited, bad key, …).</summary>
public sealed class MetadataProviderException : Exception
{
    public MetadataProviderException(string message, Exception? inner = null)
        : base(message, inner)
    {
    }
}
