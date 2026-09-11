using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using AniVault.Models;
using AniVault.Services;
using AniVault.Services.Logging;
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
    private readonly ICompressArtworkService _compressArtworkService;
    private readonly IThemeService _themeService;
    private readonly LocalizationService _loc;
    private readonly FileLoggerProvider _fileLogger;
    private readonly ILogger<SettingsViewModel> _logger;

    private bool _loaded;

    [ObservableProperty] private AppTheme _theme;
    [ObservableProperty] private AppLanguage _language;
    [ObservableProperty] private string _dataDirectory = string.Empty;
    [ObservableProperty] private string _databaseLine = string.Empty;
    [ObservableProperty] private string _configLine = string.Empty;
    [ObservableProperty] private string _mediaStoredLine = string.Empty;
    [ObservableProperty] private bool _onlineSearchEnabled;
    [ObservableProperty] private bool _posterDownloadEnabled;

    public SettingsViewModel(
        IAppPathService paths,
        IBootstrapConfigService bootstrap,
        ISettingsService settings,
        IDialogService dialogService,
        IMediaService mediaService,
        ICompressArtworkService compressArtworkService,
        IThemeService themeService,
        LocalizationService loc,
        FileLoggerProvider fileLogger,
        BackupExportViewModel dataManagement,
        MetadataSettingsViewModel metadata,
        ILogger<SettingsViewModel> logger)
    {
        _paths = paths;
        _bootstrap = bootstrap;
        _settings = settings;
        _dialogService = dialogService;
        _mediaService = mediaService;
        _compressArtworkService = compressArtworkService;
        _themeService = themeService;
        _loc = loc;
        _fileLogger = fileLogger;
        DataManagement = dataManagement;
        Metadata = metadata;
        _logger = logger;
    }

    public AppTheme[] ThemeOptions { get; } = Enum.GetValues<AppTheme>();

    public AppLanguage[] LanguageOptions { get; } = Enum.GetValues<AppLanguage>();

    /// <summary>Backup / restore / export section, kept as its own component.</summary>
    public BackupExportViewModel DataManagement { get; }

    /// <summary>Metadata provider selection & API keys, kept as its own component.</summary>
    public MetadataSettingsViewModel Metadata { get; }

    public override async Task LoadAsync()
    {
        _loaded = false;

        var notConfigured = _loc.Text("Common.NotConfigured");
        DataDirectory = _paths.DataDirectory ?? notConfigured;
        DatabaseLine = _loc.Format("Settings.DatabaseFormat", _paths.IsConfigured ? _paths.DatabaseFilePath : notConfigured);
        ConfigLine = _loc.Format("Settings.ConfigFormat", _bootstrap.ConfigFilePath);

        Theme = _themeService.Current;
        Language = _loc.Current;
        OnlineSearchEnabled = await _settings.GetBoolAsync(SettingKeys.OnlineSearchEnabled, false);
        PosterDownloadEnabled = await _settings.GetBoolAsync(SettingKeys.PosterDownloadEnabled, false);

        var total = await _mediaService.CountAsync(MediaType.Anime)
                    + await _mediaService.CountAsync(MediaType.Movie)
                    + await _mediaService.CountAsync(MediaType.TvSeries);
        MediaStoredLine = _loc.Format("Settings.MediaStoredFormat", total);

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

    partial void OnLanguageChanged(AppLanguage value)
    {
        if (_loaded)
        {
            _ = _loc.SetLanguageAsync(value);
        }
    }

    partial void OnOnlineSearchEnabledChanged(bool value)
        => PersistIfLoaded(SettingKeys.OnlineSearchEnabled, value.ToString());

    partial void OnPosterDownloadEnabledChanged(bool value)
        => PersistIfLoaded(SettingKeys.PosterDownloadEnabled, value.ToString());

    [RelayCommand]
    private void ChangeDataFolder()
    {
        var picked = _dialogService.PickFolder(_loc.Text("Settings.ChangeDataFolderPick"), _paths.DataDirectory);
        if (picked is null)
        {
            return;
        }

        if (!_dialogService.Confirm(_loc.Text("Settings.ChangeConfirm"), _loc.Text("Settings.ChangeConfirmTitle")))
        {
            return;
        }

        try
        {
            var config = _bootstrap.Load();
            config.DataDirectory = picked;
            _bootstrap.Save(config);
            _dialogService.ShowInfo(_loc.Text("Settings.ChangeDone"));
            Application_Shutdown();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to change the data folder.");
            _dialogService.ShowError(_loc.Text("Settings.ChangeFailed"));
        }
    }

    [RelayCommand]
    private void OpenDataFolder() => OpenInExplorer(_paths.DataDirectory);

    [RelayCommand]
    private void OpenLogsFolder() => OpenInExplorer(_paths.IsConfigured ? _paths.LogsDirectory : null);

    [RelayCommand]
    private void ClearLogs()
    {
        if (!_paths.IsConfigured)
        {
            _dialogService.ShowWarning(_loc.Text("Settings.FolderMissing"));
            return;
        }

        if (!_dialogService.Confirm(_loc.Text("Settings.ClearLogsConfirm"), _loc.Text("Settings.ClearLogsTitle")))
        {
            return;
        }

        try
        {
            _fileLogger.Clear();
            _logger.LogInformation("Log files cleared by the user from Settings.");
            _dialogService.ShowInfo(_loc.Text("Settings.ClearLogsDone"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clear the log files.");
            _dialogService.ShowError(_loc.Text("Settings.ClearLogsFailed"));
        }
    }

    [RelayCommand]
    private async Task CompressArtwork()
    {
        if (!_paths.IsConfigured)
        {
            _dialogService.ShowWarning(_loc.Text("Settings.FolderMissing"));
            return;
        }

        await _compressArtworkService.RunAsync();
    }

    private void PersistIfLoaded(string key, string? value)
    {
        if (_loaded)
        {
            _ = PersistAsync(key, value);
        }
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
            _dialogService.ShowWarning(_loc.Text("Settings.FolderMissing"));
            return;
        }

        Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
    }

    private static void Application_Shutdown()
        => System.Windows.Application.Current.Shutdown();
}
