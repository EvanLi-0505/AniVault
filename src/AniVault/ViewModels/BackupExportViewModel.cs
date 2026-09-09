using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using AniVault.Services;
using AniVault.Services.Backup;
using AniVault.Services.Export;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace AniVault.ViewModels;

/// <summary>
/// Backup / restore / Markdown-export actions. A self-contained component so the feature
/// can be moved to its own page later without touching Settings. All actions are offline.
/// </summary>
public sealed partial class BackupExportViewModel : ObservableObject
{
    private readonly IBackupService _backupService;
    private readonly ILibraryExportService _exportService;
    private readonly IAppPathService _paths;
    private readonly IDialogService _dialogService;
    private readonly ILogger<BackupExportViewModel> _logger;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isWorking;

    public BackupExportViewModel(
        IBackupService backupService,
        ILibraryExportService exportService,
        IAppPathService paths,
        IDialogService dialogService,
        ILogger<BackupExportViewModel> logger)
    {
        _backupService = backupService;
        _exportService = exportService;
        _paths = paths;
        _dialogService = dialogService;
        _logger = logger;
    }

    public string DefaultBackupFolder => _paths.IsConfigured ? _paths.BackupsDirectory : "(not configured)";

    [RelayCommand(CanExecute = nameof(NotWorking))]
    private async Task CreateBackup()
    {
        var folder = _dialogService.PickFolder(
            "Choose a folder for the backup (e.g. a USB drive)",
            _paths.IsConfigured ? _paths.BackupsDirectory : null);
        if (folder is null)
        {
            return;
        }

        await RunAsync("Creating backup…", async () =>
        {
            var path = await _backupService.CreateBackupAsync(folder);
            StatusMessage = $"Backup saved: {Path.GetFileName(path)}";
            if (_dialogService.Confirm($"Backup created:\n{path}\n\nOpen the folder now?", "Backup complete"))
            {
                OpenContainingFolder(path);
            }
        });
    }

    [RelayCommand(CanExecute = nameof(NotWorking))]
    private async Task RestoreBackup()
    {
        var file = _dialogService.PickFile("Choose an AniVault backup", "AniVault backup (*.zip)|*.zip");
        if (file is null)
        {
            return;
        }

        var inspection = await _backupService.InspectAsync(file);
        if (!inspection.IsValid)
        {
            _dialogService.ShowError(inspection.Error ?? "That file is not a valid AniVault backup.");
            return;
        }

        var summary = inspection.Manifest is { } m
            ? $"Created: {m.CreatedAt.LocalDateTime:yyyy-MM-dd HH:mm}\nMedia items: {m.MediaCount}\nTags: {m.TagCount}"
            : "(manifest details unavailable)";

        var proceed = _dialogService.Confirm(
            "RESTORING WILL REPLACE YOUR CURRENT LIBRARY.\n\n"
            + $"Backup: {inspection.FileName}\n{summary}\n\n"
            + "Everything currently in your data folder (database and artwork) will be overwritten. "
            + "Consider making a fresh backup first.\n\nContinue?",
            "Restore from backup");
        if (!proceed)
        {
            return;
        }

        await RunAsync("Restoring…", async () =>
        {
            await _backupService.RestoreAsync(file);
            _dialogService.ShowInfo("Restore complete. AniVault will now close — start it again to use the restored library.");
            System.Windows.Application.Current.Shutdown();
        });
    }

    [RelayCommand(CanExecute = nameof(NotWorking))]
    private void OpenBackupFolder()
    {
        if (!_paths.IsConfigured)
        {
            return;
        }

        Directory.CreateDirectory(_paths.BackupsDirectory);
        Process.Start(new ProcessStartInfo { FileName = _paths.BackupsDirectory, UseShellExecute = true });
    }

    [RelayCommand(CanExecute = nameof(NotWorking))]
    private async Task ExportMarkdown()
    {
        var path = _dialogService.PickSaveFile(
            "Export library to Markdown",
            "Markdown (*.md)|*.md",
            $"AniVault_Library_{DateTime.Now:yyyy-MM-dd}.md");
        if (path is null)
        {
            return;
        }

        await RunAsync("Exporting…", async () =>
        {
            await _exportService.ExportMarkdownAsync(path);
            StatusMessage = $"Exported: {Path.GetFileName(path)}";
            if (_dialogService.Confirm($"Markdown written to:\n{path}\n\nOpen the folder now?", "Export complete"))
            {
                OpenContainingFolder(path);
            }
        });
    }

    private bool NotWorking() => !IsWorking;

    private async Task RunAsync(string workingMessage, Func<Task> action)
    {
        IsWorking = true;
        StatusMessage = workingMessage;
        NotifyCommands();
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Backup/export action failed.");
            StatusMessage = string.Empty;
            _dialogService.ShowError("The operation failed. See the log for details.");
        }
        finally
        {
            IsWorking = false;
            NotifyCommands();
        }
    }

    private void NotifyCommands()
    {
        CreateBackupCommand.NotifyCanExecuteChanged();
        RestoreBackupCommand.NotifyCanExecuteChanged();
        OpenBackupFolderCommand.NotifyCanExecuteChanged();
        ExportMarkdownCommand.NotifyCanExecuteChanged();
    }

    private static void OpenContainingFolder(string filePath)
    {
        var folder = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(folder) && Directory.Exists(folder))
        {
            Process.Start(new ProcessStartInfo { FileName = folder, UseShellExecute = true });
        }
    }
}
