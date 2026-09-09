using System.Windows;
using Microsoft.Win32;

namespace AniVault.Services;

/// <summary>
/// Thin wrapper over the WPF/Win32 dialogs the ViewModels need, so that ViewModels
/// stay free of direct UI-shell dependencies and remain testable.
/// </summary>
public interface IDialogService
{
    /// <summary>Shows a native folder picker. Returns the selected path, or null if cancelled.</summary>
    string? PickFolder(string title, string? initialDirectory = null);

    /// <summary>Shows a native open-file dialog. Returns the selected path, or null if cancelled.</summary>
    string? PickFile(string title, string filter);

    /// <summary>Shows a native save-file dialog. Returns the chosen path, or null if cancelled.</summary>
    string? PickSaveFile(string title, string filter, string defaultFileName);

    void ShowInfo(string message, string title = "AniVault");

    void ShowWarning(string message, string title = "AniVault");

    void ShowError(string message, string title = "AniVault");

    /// <summary>Yes/No confirmation. Returns true when the user chooses Yes.</summary>
    bool Confirm(string message, string title = "AniVault");

    /// <summary>Single-line text prompt. Returns the entered text, or null if cancelled.</summary>
    string? Prompt(string title, string message, string? initialValue = null);

    /// <summary>
    /// Three-way question. Yes = <paramref name="primaryLabel"/> action, No = secondary action,
    /// Cancel = do nothing. Returns <see cref="DialogChoice"/>.
    /// </summary>
    DialogChoice AskThreeWay(string message, string title, string primaryLabel, string secondaryLabel);
}

public enum DialogChoice
{
    Primary,
    Secondary,
    Cancel,
}

public sealed class DialogService : IDialogService
{
    public string? PickFolder(string title, string? initialDirectory = null)
    {
        var dialog = new OpenFolderDialog
        {
            Title = title,
            Multiselect = false,
        };

        if (!string.IsNullOrWhiteSpace(initialDirectory))
        {
            dialog.InitialDirectory = initialDirectory;
        }

        return dialog.ShowDialog() == true ? dialog.FolderName : null;
    }

    public string? PickFile(string title, string filter)
    {
        var dialog = new OpenFileDialog
        {
            Title = title,
            Filter = filter,
            Multiselect = false,
            CheckFileExists = true,
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? PickSaveFile(string title, string filter, string defaultFileName)
    {
        var dialog = new SaveFileDialog
        {
            Title = title,
            Filter = filter,
            FileName = defaultFileName,
            OverwritePrompt = true,
            AddExtension = true,
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public void ShowInfo(string message, string title = "AniVault")
        => MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);

    public void ShowWarning(string message, string title = "AniVault")
        => MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Warning);

    public void ShowError(string message, string title = "AniVault")
        => MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);

    public bool Confirm(string message, string title = "AniVault")
        => MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;

    public string? Prompt(string title, string message, string? initialValue = null)
    {
        var window = new Views.PromptWindow(title, message, initialValue)
        {
            Owner = Application.Current.MainWindow,
        };
        return window.ShowDialog() == true ? window.Value : null;
    }

    public DialogChoice AskThreeWay(string message, string title, string primaryLabel, string secondaryLabel)
    {
        var full = $"{message}\n\nYes = {primaryLabel}\nNo = {secondaryLabel}\nCancel = do nothing";
        return MessageBox.Show(full, title, MessageBoxButton.YesNoCancel, MessageBoxImage.Question) switch
        {
            MessageBoxResult.Yes => DialogChoice.Primary,
            MessageBoxResult.No => DialogChoice.Secondary,
            _ => DialogChoice.Cancel,
        };
    }
}
