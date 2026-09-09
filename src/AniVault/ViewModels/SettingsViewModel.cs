using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using AniVault.Models;
using AniVault.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace AniVault.ViewModels;

/// <summary>
/// Settings page. The network-related options here only enable features that still
/// require an explicit user action later — nothing here starts background traffic.
/// </summary>
public sealed partial class SettingsViewModel : ViewModelBase
{
    private readonly IAppPathService _paths;
    private readonly IBootstrapConfigService _bootstrap;
    private readonly ISettingsService _settings;
    private readonly IDialogService _dialogService;
    private readonly IMediaService _mediaService;
    private readonly IThemeService _themeService;
    private readonly ILogger<SettingsViewModel> _logger;

    private bool _loaded;

    [ObservableProperty]
    private AppTheme _theme;

    [ObservableProperty]
    private string _dataDirectory = string.Empty;

    [ObservableProperty]
    private string _databaseFilePath = string.Empty;

    [ObservableProperty]
    private string _bootstrapFilePath = string.Empty;

    [ObservableProperty]
    private string _logsDirectory = string.Empty;

    [ObservableProperty]
    private int _totalMediaCount;

    [ObservableProperty]
    private bool _onlineSearchEnabled;

    [ObservableProperty]
    private bool _posterDownloadEnabled;

    public SettingsViewModel(
        IAppPathService paths,
        IBootstrapConfigService bootstrap,
        ISettingsService settings,
        IDialogService dialogService,
        IMediaService mediaService,
        IThemeService themeService,
        BackupExportViewModel dataManagement,
        MetadataSettingsViewModel metadata,
        ILogger<SettingsViewModel> logger)
    {
        _paths = paths;
        _bootstrap = bootstrap;
        _settings = settings;
        _dialogService = dialogService;
        _mediaService = mediaService;
        _themeService = themeService;
        DataManagement = dataManagement;
        Metadata = metadata;
        _logger = logger;
    }

    public AppTheme[] ThemeOptions { get; } = Enum.GetValues<AppTheme>();

    /// <summary>Backup / restore / export section, kept as its own component.</summary>
    public BackupExportViewModel DataManagement { get; }

    /// <summary>Metadata provider selection & API keys, kept as its own component.</summary>
    public MetadataSettingsViewModel Metadata { get; }

    public override async Task LoadAsync()
    {
        _loaded = false;

        DataDirectory = _paths.DataDirectory ?? "(not configured)";
        DatabaseFilePath = _paths.IsConfigured ? _paths.DatabaseFilePath : "(not configured)";
        LogsDirectory = _paths.IsConfigured ? _paths.LogsDirectory : "(not configured)";
        BootstrapFilePath = _bootstrap.ConfigFilePath;

        Theme = _themeService.Current;
        OnlineSearchEnabled = await _settings.GetBoolAsync(SettingKeys.OnlineSearchEnabled, false);
        PosterDownloadEnabled = await _settings.GetBoolAsync(SettingKeys.PosterDownloadEnabled, false);

        TotalMediaCount =
            await _mediaService.CountAsync(MediaType.Anime)
            + await _mediaService.CountAsync(MediaType.Movie)
            + await _mediaService.CountAsync(MediaType.TvSeries);

        await Metadata.LoadAsync();

        _loaded = true;
    }

    partial void OnThemeChanged(AppTheme value)
    {
        if (_loaded)
        {
            _ = _themeService.SetThemeAsync(value);
        }
    }

    partial void OnOnlineSearchEnabledChanged(bool value)
        => PersistIfLoaded(SettingKeys.OnlineSearchEnabled, value.ToString());

    partial void OnPosterDownloadEnabledChanged(bool value)
        => PersistIfLoaded(SettingKeys.PosterDownloadEnabled, value.ToString());

    [RelayCommand]
    private void ChangeDataFolder()
    {
        var picked = _dialogService.PickFolder("Choose a new AniVault data folder", _paths.DataDirectory);
        if (picked is null)
        {
            return;
        }

        var proceed = _dialogService.Confirm(
            "AniVault will point at the new folder and then close.\n\n"
            + "Your existing data is NOT moved automatically — copy the contents of the old "
            + "folder into the new one first if you want to keep it.\n\nContinue?",
            "Change data folder");
        if (!proceed)
        {
            return;
        }

        try
        {
            var config = _bootstrap.Load();
            config.DataDirectory = picked;
            _bootstrap.Save(config);
            _dialogService.ShowInfo("Data folder updated. AniVault will now close; start it again to continue.");
            Application_Shutdown();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to change the data folder.");
            _dialogService.ShowError("Could not update the data folder. See the log for details.");
        }
    }

    [RelayCommand]
    private void OpenDataFolder() => OpenInExplorer(_paths.DataDirectory);

    [RelayCommand]
    private void OpenLogsFolder() => OpenInExplorer(_paths.IsConfigured ? _paths.LogsDirectory : null);

    private void PersistIfLoaded(string key, string? value)
    {
        if (!_loaded)
        {
            return;
        }

        _ = PersistAsync(key, value);
    }

    private async Task PersistAsync(string key, string? value)
    {
        try
        {
            await _settings.SetAsync(key, value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save setting {Key}.", key);
        }
    }

    private void OpenInExplorer(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
        {
            _dialogService.ShowWarning("That folder does not exist yet.");
            return;
        }

        Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
    }

    private static void Application_Shutdown()
        => System.Windows.Application.Current.Shutdown();
}
