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
    private readonly ILocalizationService _loc;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isWorking;

    public BackupExportViewModel(
        IBackupService backupService,
        ILibraryExportService exportService,
        IAppPathService paths,
        IDialogService dialogService,
        ILocalizationService loc,
        ILogger<BackupExportViewModel> logger)
    {
        _backupService = backupService;
        _exportService = exportService;
        _paths = paths;
        _dialogService = dialogService;
        _loc = loc;
        _logger = logger;
    }

    public string DefaultBackupFolder => _paths.IsConfigured ? _paths.BackupsDirectory : _loc.Text("Common.NotConfigured");

    [RelayCommand(CanExecute = nameof(NotWorking))]
    private async Task CreateBackup()
    {
        var folder = _dialogService.PickFolder(
            _loc.Text("Backup.ChooseFolder"),
            _paths.IsConfigured ? _paths.BackupsDirectory : null);
        if (folder is null)
        {
            return;
        }

        await RunAsync(_loc.Text("Backup.Creating"), async () =>
        {
            var path = await _backupService.CreateBackupAsync(folder);
            StatusMessage = _loc.Format("Backup.SavedFormat", Path.GetFileName(path));
            if (_dialogService.Confirm(_loc.Format("Backup.CompleteFormat", path), _loc.Text("Backup.CompleteTitle")))
            {
                OpenContainingFolder(path);
            }
        });
    }

    [RelayCommand(CanExecute = nameof(NotWorking))]
    private async Task RestoreBackup()
    {
        var file = _dialogService.PickFile(_loc.Text("Backup.ChooseBackup"), "AniVault backup (*.zip)|*.zip");
        if (file is null)
        {
            return;
        }

        var inspection = await _backupService.InspectAsync(file);
        if (!inspection.IsValid)
        {
            _dialogService.ShowError(inspection.Error ?? _loc.Text("Backup.InvalidFormat"));
            return;
        }

        var summary = inspection.Manifest is { } m
            ? _loc.Format("Backup.ManifestFormat", m.CreatedAt.LocalDateTime.ToString("yyyy-MM-dd HH:mm"), m.MediaCount, m.TagCount)
            : _loc.Text("Backup.ManifestUnavailable");

        var proceed = _dialogService.Confirm(
            _loc.Format("Backup.RestoreWarnFormat", inspection.FileName, summary),
            _loc.Text("Backup.RestoreTitle"));
        if (!proceed)
        {
            return;
        }

        await RunAsync(_loc.Text("Backup.Restoring"), async () =>
        {
            await _backupService.RestoreAsync(file);
            _dialogService.ShowInfo(_loc.Text("Backup.RestoreDone"));
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
            _loc.Text("Backup.ExportSaveTitle"),
            "Markdown (*.md)|*.md",
            $"AniVault_Library_{DateTime.Now:yyyy-MM-dd}.md");
        if (path is null)
        {
            return;
        }

        await RunAsync(_loc.Text("Backup.Exporting"), async () =>
        {
            await _exportService.ExportMarkdownAsync(path);
            StatusMessage = _loc.Format("Backup.ExportedFormat", Path.GetFileName(path));
            if (_dialogService.Confirm(_loc.Format("Backup.ExportCompleteFormat", path), _loc.Text("Backup.ExportCompleteTitle")))
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
            _dialogService.ShowError(_loc.Text("Backup.Failed"));
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
