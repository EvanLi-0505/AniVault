using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using AniVault.Services;
using AniVault.Services.Artwork;
using AniVault.Utilities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace AniVault.ViewModels;

/// <summary>One oversized poster/backdrop the user can choose to compress.</summary>
public sealed partial class OversizedArtworkRowViewModel : ObservableObject
{
    private readonly Action _onSelectionChanged;

    [ObservableProperty] private bool _isSelected = true;

    public OversizedArtworkRowViewModel(OversizedArtworkItem item, ILocalizationService loc, Action onSelectionChanged)
    {
        Item = item;
        KindLabel = item.IsPoster ? loc.Text("CompressArtwork.PosterKind") : loc.Text("CompressArtwork.BackdropKind");
        SizeLabel = loc.Format("CompressArtwork.SizeFormat", item.PixelWidth, ByteSize.Format(item.FileSizeBytes));
        _onSelectionChanged = onSelectionChanged;
    }

    public OversizedArtworkItem Item { get; }

    public string Title => Item.Title;

    public string KindLabel { get; }

    public string SizeLabel { get; }

    partial void OnIsSelectedChanged(bool value) => _onSelectionChanged();
}

/// <summary>
/// Backs the modal "Compress oversized artwork" window: scans the library for posters/backdrops
/// wider than the app ever displays and lets the user pick exactly which ones to shrink.
/// Nothing is downloaded or modified just by opening this — compression only runs for rows the
/// user leaves checked when they press the button.
/// </summary>
public sealed partial class CompressArtworkViewModel : ObservableObject
{
    private readonly IArtworkService _artwork;
    private readonly IDialogService _dialogService;
    private readonly ILocalizationService _loc;
    private readonly ILogger<CompressArtworkViewModel> _logger;

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _isEmpty;

    public CompressArtworkViewModel(
        IArtworkService artwork, IDialogService dialogService, ILocalizationService loc, ILogger<CompressArtworkViewModel> logger)
    {
        _artwork = artwork;
        _dialogService = dialogService;
        _loc = loc;
        _logger = logger;
    }

    /// <summary>Raised when the window should close.</summary>
    public event Action? RequestClose;

    public ObservableCollection<OversizedArtworkRowViewModel> Items { get; } = new();

    public int SelectedCount => Items.Count(i => i.IsSelected);

    public string CompressButtonLabel => _loc.Format("CompressArtwork.CompressSelectedFormat", SelectedCount);

    public async Task LoadAsync()
    {
        IsBusy = true;
        try
        {
            Items.Clear();
            var found = await _artwork.FindOversizedArtworkAsync();
            foreach (var item in found.OrderByDescending(i => i.FileSizeBytes))
            {
                Items.Add(new OversizedArtworkRowViewModel(item, _loc, RaiseSelectionChanged));
            }

            IsEmpty = Items.Count == 0;
            RaiseSelectionChanged();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to scan for oversized artwork.");
            _dialogService.ShowError(_loc.Text("CompressArtwork.ScanFailed"));
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void SelectAll()
    {
        foreach (var item in Items)
        {
            item.IsSelected = true;
        }
    }

    [RelayCommand]
    private void SelectNone()
    {
        foreach (var item in Items)
        {
            item.IsSelected = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanCompress))]
    private async Task Compress()
    {
        var selected = Items.Where(i => i.IsSelected).Select(i => i.Item).ToList();
        if (selected.Count == 0)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _artwork.CompressArtworkAsync(selected);
            _logger.LogInformation(
                "Compressed {Count} artwork file(s) the user selected, saved {Bytes} bytes.",
                result.FilesCompressed, result.BytesSaved);
            _dialogService.ShowInfo(result.FilesCompressed == 0
                ? _loc.Text("CompressArtwork.NoneCompressed")
                : _loc.Format("CompressArtwork.DoneFormat", result.FilesCompressed, ByteSize.Format(result.BytesSaved)));
            RequestClose?.Invoke();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to compress the selected artwork.");
            _dialogService.ShowError(_loc.Text("CompressArtwork.CompressFailed"));
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanCompress() => !IsBusy && SelectedCount > 0;

    [RelayCommand]
    private void Close() => RequestClose?.Invoke();

    private void RaiseSelectionChanged()
    {
        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(CompressButtonLabel));
        CompressCommand.NotifyCanExecuteChanged();
    }
}
