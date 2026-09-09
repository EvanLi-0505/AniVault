using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using AniVault.Metadata;
using AniVault.Models;
using AniVault.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace AniVault.ViewModels;

/// <summary>One metadata provider row in Settings.</summary>
public sealed partial class MetadataProviderRow : ObservableObject
{
    [ObservableProperty] private bool _isActive;
    [ObservableProperty] private bool _hasApiKey;
    [ObservableProperty] private string _apiKeyInput = string.Empty;

    public MetadataProviderRow(MetadataProviderInfo info)
    {
        Source = info.Source;
        DisplayName = info.DisplayName;
        Description = info.Description;
        RequiresApiKey = info.RequiresApiKey;
        HasApiKey = info.HasApiKey;
    }

    public ExternalSource Source { get; }

    public string DisplayName { get; }

    public string Description { get; }

    public bool RequiresApiKey { get; }
}

/// <summary>
/// Settings component for the online metadata system: pick the active provider and manage
/// any API keys. Self-contained so it can move to its own page later.
/// </summary>
public sealed partial class MetadataSettingsViewModel : ObservableObject
{
    private readonly IMetadataService _metadata;
    private readonly IDialogService _dialog;
    private readonly ILogger<MetadataSettingsViewModel> _logger;

    private bool _loaded;

    [ObservableProperty] private MetadataProviderRow? _activeProvider;

    public MetadataSettingsViewModel(
        IMetadataService metadata,
        IDialogService dialog,
        ILogger<MetadataSettingsViewModel> logger)
    {
        _metadata = metadata;
        _dialog = dialog;
        _logger = logger;
    }

    public ObservableCollection<MetadataProviderRow> Providers { get; } = new();

    public async Task LoadAsync()
    {
        _loaded = false;
        Providers.Clear();

        var active = await _metadata.GetActiveProviderAsync();
        foreach (var info in await _metadata.GetProvidersAsync())
        {
            var row = new MetadataProviderRow(info) { IsActive = info.Source == active };
            Providers.Add(row);
        }

        ActiveProvider = Providers.FirstOrDefault(p => p.IsActive) ?? Providers.FirstOrDefault();
        _loaded = true;
    }

    partial void OnActiveProviderChanged(MetadataProviderRow? value)
    {
        if (!_loaded || value is null)
        {
            return;
        }

        foreach (var row in Providers)
        {
            row.IsActive = ReferenceEquals(row, value);
        }

        _ = PersistActiveAsync(value.Source);
    }

    [RelayCommand]
    private void SelectProvider(MetadataProviderRow? row)
    {
        if (row is not null)
        {
            ActiveProvider = row;
        }
    }

    [RelayCommand]
    private async Task SaveApiKey(MetadataProviderRow? row)
    {
        if (row is null || string.IsNullOrWhiteSpace(row.ApiKeyInput))
        {
            return;
        }

        try
        {
            await _metadata.SetApiKeyAsync(row.Source, row.ApiKeyInput.Trim());
            row.ApiKeyInput = string.Empty;
            row.HasApiKey = true;
            _dialog.ShowInfo($"{row.DisplayName} API key saved.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save API key for {Provider}.", row.Source);
            _dialog.ShowError("Could not save the API key.");
        }
    }

    [RelayCommand]
    private async Task ClearApiKey(MetadataProviderRow? row)
    {
        if (row is null || !_dialog.Confirm($"Remove the saved {row.DisplayName} API key?", "Clear API key"))
        {
            return;
        }

        await _metadata.SetApiKeyAsync(row.Source, null);
        row.HasApiKey = false;
    }

    private async Task PersistActiveAsync(ExternalSource source)
    {
        try
        {
            await _metadata.SetActiveProviderAsync(source);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set active metadata provider.");
        }
    }
}
