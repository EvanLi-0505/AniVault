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

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ContinueCommand))]
    private string _selectedFolder = string.Empty;

    [ObservableProperty]
    private string? _statusMessage;

    public FirstRunViewModel(
        IAppPathService paths,
        IDialogService dialogService,
        DatabaseInitializer databaseInitializer,
        ILogger<FirstRunViewModel> logger)
    {
        _paths = paths;
        _dialogService = dialogService;
        _databaseInitializer = databaseInitializer;
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
        var picked = _dialogService.PickFolder("Choose where AniVault should store your library", SelectedFolder);
        if (picked is not null)
        {
            SelectedFolder = picked;
        }
    }

    [RelayCommand(CanExecute = nameof(CanContinue))]
    private async Task Continue()
    {
        StatusMessage = "Creating your library…";
        try
        {
            Directory.CreateDirectory(SelectedFolder);

            if (!IsDirectoryWritable(SelectedFolder))
            {
                StatusMessage = null;
                _dialogService.ShowError("That folder is not writable. Please choose another location.");
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
            _dialogService.ShowError("Setup failed. See the log for details, then try a different folder.");
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
