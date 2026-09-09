using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AniVault.Metadata;
using AniVault.Models;
using AniVault.Services;
using AniVault.Utilities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace AniVault.ViewModels;

/// <summary>A provider option in the online-search picker.</summary>
public sealed record ProviderChoice(ExternalSource Source, string DisplayName, bool IsReady, string Description);

/// <summary>One online search result row.</summary>
public sealed class OnlineResultViewModel
{
    public OnlineResultViewModel(MetadataSearchResult result)
    {
        Result = result;
        TypeLabel = EnumDisplay.Label(result.MediaType);
    }

    public MetadataSearchResult Result { get; }

    public string Title => Result.Title;

    public string? SecondaryTitle => Result.SecondaryTitle;

    public string? YearLabel => Result.Year?.ToString();

    public string TypeLabel { get; }

    public string? PosterUrl => Result.PosterUrl;

    public string? Summary => Result.Summary;
}

/// <summary>
/// Backs the modal "Search online" window: pick a provider, search, preview a result, then
/// explicitly add it to the local library. Every network call here is triggered by a button.
/// </summary>
public sealed partial class OnlineSearchViewModel : ObservableObject
{
    private readonly IMetadataService _metadata;
    private readonly IMetadataImporter _importer;
    private readonly ISettingsService _settings;
    private readonly IDialogService _dialog;
    private readonly ILogger<OnlineSearchViewModel> _logger;

    private CancellationTokenSource? _cts;

    [ObservableProperty] private string _query = string.Empty;
    [ObservableProperty] private ProviderChoice? _selectedProvider;
    [ObservableProperty] private MediaType? _preferredMediaType;
    [ObservableProperty] private OnlineResultViewModel? _selectedResult;
    [ObservableProperty] private MediaMetadata? _preview;
    [ObservableProperty] private bool _isSearching;
    [ObservableProperty] private bool _isLoadingPreview;
    [ObservableProperty] private bool _isImporting;
    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private bool _posterDownloadAllowed;
    [ObservableProperty] private bool _downloadPoster;
    [ObservableProperty] private bool _downloadBackdrop;
    [ObservableProperty] private bool _importGenresAsTags = true;

    public OnlineSearchViewModel(
        IMetadataService metadata,
        IMetadataImporter importer,
        ISettingsService settings,
        IDialogService dialog,
        ILogger<OnlineSearchViewModel> logger)
    {
        _metadata = metadata;
        _importer = importer;
        _settings = settings;
        _dialog = dialog;
        _logger = logger;
    }

    public event Action? RequestClose;

    public int? ImportedMediaId { get; private set; }

    public ObservableCollection<ProviderChoice> Providers { get; } = new();

    public ObservableCollection<OnlineResultViewModel> Results { get; } = new();

    public string HeaderText => PreferredMediaType is { } t
        ? $"Search online for {EnumDisplay.Label(t)}"
        : "Search online";

    public bool HasPreview => Preview is not null;

    // ---- Preview formatting ------------------------------------------------

    public string? PreviewTitle => Preview?.Title;
    public string? PreviewOriginalTitle => Preview?.OriginalTitle;
    public string? PreviewDescription => Preview?.Description;
    public string? PreviewPosterUrl => Preview?.PosterUrl;

    public string? PreviewFacts
    {
        get
        {
            if (Preview is not { } p)
            {
                return null;
            }

            var parts = new List<string> { EnumDisplay.Label(p.MediaType) };
            if (p.AirYear is { } year)
            {
                parts.Add(p.AirSeason is { } s ? $"{year} · {EnumDisplay.Label(s)}" : year.ToString());
            }

            if (p.EpisodeCount is { } eps and > 0)
            {
                parts.Add($"{eps} episodes");
            }

            if (p.RuntimeMinutes is { } rt and > 0)
            {
                parts.Add($"{rt} min");
            }

            if (p.ProviderRating is { } rating and > 0)
            {
                parts.Add($"provider score {rating:0.0}");
            }

            return string.Join("  ·  ", parts);
        }
    }

    public string? PreviewGenres => Preview is { Genres.Count: > 0 } p ? string.Join(", ", p.Genres) : null;

    public async Task InitializeAsync(MediaType? preferredMediaType)
    {
        PreferredMediaType = preferredMediaType;
        OnPropertyChanged(nameof(HeaderText));

        PosterDownloadAllowed = await _settings.GetBoolAsync(SettingKeys.PosterDownloadEnabled, false);
        DownloadPoster = PosterDownloadAllowed;
        DownloadBackdrop = false;

        Providers.Clear();
        var infos = await _metadata.GetProvidersAsync();
        foreach (var info in infos)
        {
            Providers.Add(new ProviderChoice(info.Source, info.DisplayName, info.IsReady, info.Description));
        }

        var active = await _metadata.GetActiveProviderAsync();
        SelectedProvider =
            Providers.FirstOrDefault(p => p.Source == active && p.IsReady)
            ?? Providers.FirstOrDefault(p => p.IsReady)
            ?? Providers.FirstOrDefault();
    }

    partial void OnSelectedResultChanged(OnlineResultViewModel? value)
    {
        _ = LoadPreviewAsync(value);
    }

    partial void OnPreviewChanged(MediaMetadata? value)
    {
        OnPropertyChanged(nameof(HasPreview));
        OnPropertyChanged(nameof(PreviewTitle));
        OnPropertyChanged(nameof(PreviewOriginalTitle));
        OnPropertyChanged(nameof(PreviewDescription));
        OnPropertyChanged(nameof(PreviewPosterUrl));
        OnPropertyChanged(nameof(PreviewFacts));
        OnPropertyChanged(nameof(PreviewGenres));
        AddToLibraryCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private async Task Search()
    {
        if (SelectedProvider is not { } provider)
        {
            return;
        }

        if (!provider.IsReady)
        {
            StatusMessage = $"{provider.DisplayName} needs an API key. Add it in Settings → Metadata.";
            return;
        }

        if (string.IsNullOrWhiteSpace(Query))
        {
            return;
        }

        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        IsSearching = true;
        StatusMessage = null;
        Results.Clear();
        Preview = null;

        try
        {
            var found = await _metadata.SearchAsync(provider.Source, Query, PreferredMediaType, _cts.Token);
            foreach (var result in found)
            {
                Results.Add(new OnlineResultViewModel(result));
            }

            StatusMessage = Results.Count == 0 ? "No results. Try a different query or provider." : null;
        }
        catch (OperationCanceledException)
        {
        }
        catch (MetadataProviderException ex)
        {
            StatusMessage = ex.Message;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Online search failed.");
            StatusMessage = "The search failed unexpectedly. Your local library is unaffected.";
        }
        finally
        {
            IsSearching = false;
        }
    }

    private async Task LoadPreviewAsync(OnlineResultViewModel? result)
    {
        Preview = null;
        if (result is null || SelectedProvider is not { } provider)
        {
            return;
        }

        IsLoadingPreview = true;
        StatusMessage = null;
        try
        {
            Preview = await _metadata.GetDetailsAsync(provider.Source, result.Result.ExternalId, CancellationToken.None);
            if (Preview is null)
            {
                StatusMessage = "That result has no details available.";
            }
        }
        catch (MetadataProviderException ex)
        {
            StatusMessage = ex.Message;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load online preview.");
            StatusMessage = "Could not load that result's details.";
        }
        finally
        {
            IsLoadingPreview = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanImport))]
    private async Task AddToLibrary()
    {
        if (Preview is not { } metadata)
        {
            return;
        }

        IsImporting = true;
        try
        {
            var duplicate = await _importer.CheckDuplicateAsync(metadata);
            if (duplicate is { IsPossibleDuplicate: true, ExistingMediaId: { } existingId })
            {
                var choice = _dialog.AskThreeWay(
                    duplicate.Reason ?? "This item may already be in your library.",
                    "Possible duplicate", "open the existing item", "add it anyway");

                if (choice == DialogChoice.Cancel)
                {
                    return;
                }

                if (choice == DialogChoice.Primary)
                {
                    ImportedMediaId = existingId;
                    RequestClose?.Invoke();
                    return;
                }
            }

            var options = new MetadataImportOptions(
                ImportGenresAsTags,
                DownloadPoster && PosterDownloadAllowed,
                DownloadBackdrop && PosterDownloadAllowed);

            ImportedMediaId = await _importer.ImportAsync(metadata, options);
            RequestClose?.Invoke();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Import from online result failed.");
            _dialog.ShowError("Could not add that item. See the log for details.");
        }
        finally
        {
            IsImporting = false;
        }
    }

    private bool CanImport() => Preview is not null && !IsImporting;

    [RelayCommand]
    private void Cancel()
    {
        _cts?.Cancel();
        RequestClose?.Invoke();
    }
}
