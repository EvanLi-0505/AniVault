using System;
using System.Threading.Tasks;
using AniVault.Metadata;
using AniVault.Metadata.Providers;
using AniVault.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace AniVault.ViewModels;

/// <summary>
/// Backs the "Edit custom provider" dialog. The user describes one REST API — a search URL
/// with a <c>{query}</c> token and dotted JSON field paths — and it becomes a selectable
/// metadata provider. Everything is stored locally; nothing is contacted here.
/// </summary>
public sealed partial class CustomProviderViewModel : ObservableObject
{
    private readonly ICustomProviderStore _store;
    private readonly ISecureSettingsService _secureSettings;
    private readonly IDialogService _dialog;
    private readonly ILocalizationService _loc;
    private readonly ILogger<CustomProviderViewModel> _logger;

    [ObservableProperty] private CustomProviderConfig _config = new();
    [ObservableProperty] private string _apiKeyInput = string.Empty;
    [ObservableProperty] private bool _hasApiKey;

    public CustomProviderViewModel(
        ICustomProviderStore store,
        ISecureSettingsService secureSettings,
        IDialogService dialog,
        ILocalizationService loc,
        ILogger<CustomProviderViewModel> logger)
    {
        _store = store;
        _secureSettings = secureSettings;
        _dialog = dialog;
        _loc = loc;
        _logger = logger;
    }

    /// <summary>Raised when the window should close. Argument is true when the config was saved or cleared.</summary>
    public event Action<bool>? RequestClose;

    public string[] MediaTypeOptions { get; } = { "Anime", "Movie", "TvSeries" };

    public async Task LoadAsync()
    {
        Config = _store.Current.Clone();
        HasApiKey = !string.IsNullOrWhiteSpace(await _secureSettings.GetAsync(CustomMetadataProvider.ApiKeySettingKey));
        ApiKeyInput = string.Empty;
    }

    [RelayCommand]
    private async Task Save()
    {
        var config = Config;
        config.Name = config.Name?.Trim() ?? string.Empty;
        config.SearchUrl = config.SearchUrl?.Trim() ?? string.Empty;

        if (!config.IsConfigured)
        {
            _dialog.ShowWarning(_loc.Text("CustomProvider.Invalid"));
            return;
        }

        try
        {
            await _store.SaveAsync(config);

            if (!string.IsNullOrWhiteSpace(ApiKeyInput))
            {
                await _secureSettings.SetAsync(CustomMetadataProvider.ApiKeySettingKey, ApiKeyInput.Trim());
            }

            RequestClose?.Invoke(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save the custom provider config.");
            _dialog.ShowError(_loc.Text("CustomProvider.SaveFailed"));
        }
    }

    [RelayCommand]
    private async Task ClearApiKey()
    {
        await _secureSettings.SetAsync(CustomMetadataProvider.ApiKeySettingKey, null);
        HasApiKey = false;
        ApiKeyInput = string.Empty;
    }

    [RelayCommand]
    private async Task Delete()
    {
        if (!_dialog.Confirm(_loc.Text("CustomProvider.DeleteConfirm"), _loc.Text("CustomProvider.DeleteTitle")))
        {
            return;
        }

        await _store.SaveAsync(new CustomProviderConfig());
        await _secureSettings.SetAsync(CustomMetadataProvider.ApiKeySettingKey, null);
        RequestClose?.Invoke(true);
    }

    [RelayCommand]
    private void Cancel() => RequestClose?.Invoke(false);
}
