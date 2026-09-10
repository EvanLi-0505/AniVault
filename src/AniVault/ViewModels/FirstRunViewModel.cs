using System;
using System.IO;
using System.Threading.Tasks;
using AniVault.Data;
using AniVault.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace AniVault.ViewModels;

/// <summary>
/// First-run setup. Runs completely offline: the user picks a data folder, and AniVault
/// creates the directory layout and the SQLite database there.
/// </summary>
public sealed partial class FirstRunViewModel : ObservableObject
{
    private readonly IAppPathService _paths;
    private readonly IDialogService _dialogService;
    private readonly DatabaseInitializer _databaseInitializer;
    private readonly ILogger<FirstRunViewModel> _logger;
    private readonly ILocalizationService _loc;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ContinueCommand))]
    private string _selectedFolder = string.Empty;

    [ObservableProperty]
    private string? _statusMessage;

    public FirstRunViewModel(
        IAppPathService paths,
        IDialogService dialogService,
        DatabaseInitializer databaseInitializer,
        ILocalizationService loc,
        ILogger<FirstRunViewModel> logger)
    {
        _paths = paths;
        _dialogService = dialogService;
        _databaseInitializer = databaseInitializer;
        _loc = loc;
        _logger = logger;

        var suggested = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "AniVaultData");
        SelectedFolder = suggested;
    }

    /// <summary>Raised with true once setup has completed successfully.</summary>
    public event Action<bool>? SetupCompleted;

    [RelayCommand]
    private void Browse()
    {
        var picked = _dialogService.PickFolder(_loc.Text("FirstRun.BrowseTitle"), SelectedFolder);
        if (picked is not null)
        {
            SelectedFolder = picked;
        }
    }

    [RelayCommand(CanExecute = nameof(CanContinue))]
    private async Task Continue()
    {
        StatusMessage = _loc.Text("FirstRun.Creating");
        try
        {
            Directory.CreateDirectory(SelectedFolder);

            if (!IsDirectoryWritable(SelectedFolder))
            {
                StatusMessage = null;
                _dialogService.ShowError(_loc.Text("FirstRun.FolderNotWritable"));
                return;
            }

            _paths.SetDataDirectory(SelectedFolder);
            _paths.EnsureDirectoryStructure();

            await _databaseInitializer.InitializeAsync();

            _logger.LogInformation("First-run setup completed. Data directory: {Path}", SelectedFolder);
            SetupCompleted?.Invoke(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "First-run setup failed for {Path}.", SelectedFolder);
            StatusMessage = null;
            _dialogService.ShowError(_loc.Text("FirstRun.SetupFailed"));
        }
    }

    private bool CanContinue() => !string.IsNullOrWhiteSpace(SelectedFolder);

    private static bool IsDirectoryWritable(string path)
    {
        try
        {
            var probe = Path.Combine(path, ".anivault-write-test");
            File.WriteAllText(probe, string.Empty);
            File.Delete(probe);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
