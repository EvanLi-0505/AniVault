using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using AniVault.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace AniVault.ViewModels;

/// <summary>One row on the Tags page.</summary>
public sealed partial class TagRowViewModel : ObservableObject
{
    [ObservableProperty] private string _name;

    public TagRowViewModel(TagUsage usage)
    {
        Id = usage.Id;
        _name = usage.Name;
        MediaCount = usage.MediaCount;
    }

    public int Id { get; }

    public int MediaCount { get; }

    public string UsageLabel => MediaCount == 1 ? "1 item" : $"{MediaCount} items";
}

/// <summary>
/// Tag management page: list every tag with its usage count, rename, delete, remove unused,
/// or open a tag as a filtered library view.
/// </summary>
public sealed partial class TagsViewModel : ViewModelBase
{
    private readonly ITagService _tagService;
    private readonly IDialogService _dialogService;
    private readonly INavigationService _navigation;
    private readonly ILogger<TagsViewModel> _logger;

    [ObservableProperty] private bool _isEmpty;
    [ObservableProperty] private string _newTagName = string.Empty;

    public TagsViewModel(
        ITagService tagService,
        IDialogService dialogService,
        INavigationService navigation,
        ILogger<TagsViewModel> logger)
    {
        _tagService = tagService;
        _dialogService = dialogService;
        _navigation = navigation;
        _logger = logger;
    }

    public ObservableCollection<TagRowViewModel> Tags { get; } = new();

    public override async Task LoadAsync()
    {
        IsBusy = true;
        try
        {
            Tags.Clear();
            foreach (var usage in await _tagService.GetAllWithUsageAsync())
            {
                Tags.Add(new TagRowViewModel(usage));
            }

            IsEmpty = Tags.Count == 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load tags.");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task CreateTag()
    {
        var name = NewTagName.Trim();
        if (name.Length == 0)
        {
            return;
        }

        try
        {
            await _tagService.GetOrCreateAsync(name);
            NewTagName = string.Empty;
            await LoadAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create tag '{Tag}'.", name);
            _dialogService.ShowError("Could not create that tag.");
        }
    }

    [RelayCommand]
    private async Task Rename(TagRowViewModel? row)
    {
        if (row is null)
        {
            return;
        }

        var input = _dialogService.Prompt("Rename tag", "New name:", row.Name);
        if (string.IsNullOrWhiteSpace(input) || input.Trim() == row.Name)
        {
            return;
        }

        try
        {
            await _tagService.RenameAsync(row.Id, input.Trim());
            await LoadAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to rename tag {TagId}.", row.Id);
            _dialogService.ShowError(ex is InvalidOperationException ? ex.Message : "Could not rename that tag.");
        }
    }

    [RelayCommand]
    private async Task Delete(TagRowViewModel? row)
    {
        if (row is null)
        {
            return;
        }

        var message = row.MediaCount > 0
            ? $"Delete the tag \"{row.Name}\"? It will be removed from {row.UsageLabel}."
            : $"Delete the unused tag \"{row.Name}\"?";
        if (!_dialogService.Confirm(message, "Delete tag"))
        {
            return;
        }

        await _tagService.DeleteAsync(row.Id);
        await LoadAsync();
    }

    [RelayCommand]
    private async Task DeleteUnused()
    {
        if (!_dialogService.Confirm("Delete every tag that is not attached to any media?", "Clean up tags"))
        {
            return;
        }

        var removed = await _tagService.DeleteUnusedAsync();
        _dialogService.ShowInfo(removed == 0 ? "There were no unused tags." : $"Removed {removed} unused tag(s).");
        await LoadAsync();
    }

    [RelayCommand]
    private void OpenTag(TagRowViewModel? row)
    {
        if (row is null)
        {
            return;
        }

        _navigation.NavigateToDetail<LibraryViewModel>(vm => vm.Configure(new LibraryPreset(
            $"Tag: {row.Name}",
            "Every media item with this tag.",
            TagId: row.Id)));
    }
}
