using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AniVault.Metadata.Providers;
using AniVault.Models;
using AniVault.Services;

namespace AniVault.Metadata;

/// <summary>Everything the UI needs to know about one provider to show it in Settings / the picker.</summary>
public sealed record MetadataProviderInfo(
    ExternalSource Source,
    string DisplayName,
    string Description,
    bool RequiresApiKey,
    bool HasApiKey,
    bool IsReady);

/// <summary>
/// Front door to online metadata. Chooses the active provider, isolates provider-specific
/// configuration (API keys), and enforces the rule that a network call only happens on an
/// explicit user action while "online metadata search" is enabled in Settings.
/// </summary>
public interface IMetadataService
{
    Task<IReadOnlyList<MetadataProviderInfo>> GetProvidersAsync(CancellationToken cancellationToken = default);

    Task<ExternalSource> GetActiveProviderAsync(CancellationToken cancellationToken = default);

    Task SetActiveProviderAsync(ExternalSource source, CancellationToken cancellationToken = default);

    Task<bool> IsOnlineSearchEnabledAsync(CancellationToken cancellationToken = default);

    Task<string?> GetApiKeyAsync(ExternalSource source, CancellationToken cancellationToken = default);

    Task SetApiKeyAsync(ExternalSource source, string? apiKey, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MetadataSearchResult>> SearchAsync(
        ExternalSource source, string query, MediaType? preferredMediaType, CancellationToken cancellationToken);

    Task<MediaMetadata?> GetDetailsAsync(
        ExternalSource source, string externalId, CancellationToken cancellationToken);
}

public sealed class MetadataService : IMetadataService
{
    private readonly IReadOnlyDictionary<ExternalSource, IMetadataProvider> _providers;
    private readonly ISettingsService _settings;
    private readonly ISecureSettingsService _secureSettings;

    public MetadataService(
        IEnumerable<IMetadataProvider> providers,
        ISettingsService settings,
        ISecureSettingsService secureSettings)
    {
        _providers = providers.ToDictionary(p => p.Source);
        _settings = settings;
        _secureSettings = secureSettings;
    }

    public async Task<IReadOnlyList<MetadataProviderInfo>> GetProvidersAsync(CancellationToken cancellationToken = default)
    {
        var infos = new List<MetadataProviderInfo>();
        foreach (var provider in _providers.Values.OrderBy(p => p.DisplayName))
        {
            var hasKey = !provider.RequiresApiKey || !string.IsNullOrWhiteSpace(await GetApiKeyAsync(provider.Source, cancellationToken));
            infos.Add(new MetadataProviderInfo(
                provider.Source, provider.DisplayName, provider.Description,
                provider.RequiresApiKey, hasKey, IsReady: hasKey && provider.IsConfigured));
        }

        return infos;
    }

    public async Task<ExternalSource> GetActiveProviderAsync(CancellationToken cancellationToken = default)
    {
        var stored = await _settings.GetAsync(SettingKeys.MetadataProvider, cancellationToken);
        if (Enum.TryParse<ExternalSource>(stored, ignoreCase: true, out var source) && _providers.ContainsKey(source))
        {
            return source;
        }

        // Default to Bangumi (Chinese titles, no key). The user can switch in Settings.
        return _providers.ContainsKey(ExternalSource.Bangumi) ? ExternalSource.Bangumi : _providers.Keys.First();
    }

    public Task SetActiveProviderAsync(ExternalSource source, CancellationToken cancellationToken = default)
        => _settings.SetAsync(SettingKeys.MetadataProvider, source.ToString(), cancellationToken);

    public Task<bool> IsOnlineSearchEnabledAsync(CancellationToken cancellationToken = default)
        => _settings.GetBoolAsync(SettingKeys.OnlineSearchEnabled, false, cancellationToken);

    public Task<string?> GetApiKeyAsync(ExternalSource source, CancellationToken cancellationToken = default)
        => _secureSettings.GetAsync(ApiKeySettingKey(source), cancellationToken);

    public Task SetApiKeyAsync(ExternalSource source, string? apiKey, CancellationToken cancellationToken = default)
        => _secureSettings.SetAsync(ApiKeySettingKey(source), apiKey, cancellationToken);

    public async Task<IReadOnlyList<MetadataSearchResult>> SearchAsync(
        ExternalSource source, string query, MediaType? preferredMediaType, CancellationToken cancellationToken)
    {
        await EnsureAllowedAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(query))
        {
            return Array.Empty<MetadataSearchResult>();
        }

        return await Provider(source).SearchAsync(query.Trim(), preferredMediaType, cancellationToken);
    }

    public async Task<MediaMetadata?> GetDetailsAsync(
        ExternalSource source, string externalId, CancellationToken cancellationToken)
    {
        await EnsureAllowedAsync(cancellationToken);
        return await Provider(source).GetDetailsAsync(externalId, cancellationToken);
    }

    private async Task EnsureAllowedAsync(CancellationToken cancellationToken)
    {
        if (!await IsOnlineSearchEnabledAsync(cancellationToken))
        {
            throw new MetadataProviderException("Online metadata search is turned off. Enable it in Settings first.");
        }
    }

    private IMetadataProvider Provider(ExternalSource source)
        => _providers.TryGetValue(source, out var provider)
            ? provider
            : throw new MetadataProviderException($"The {source} provider is not available.");

    private static string ApiKeySettingKey(ExternalSource source) => source switch
    {
        ExternalSource.Tmdb => TmdbProvider.ApiKeySettingKey,
        _ => $"metadata.apikey.{source.ToString().ToLowerInvariant()}",
    };
}
