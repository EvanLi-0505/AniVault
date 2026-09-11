using System;
using System.Collections.ObjectModel;
using System.Linq;
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

    public string UsageLabel => MediaCount == 1
        ? LocalizationService.Instance?.Text("Tags.ItemCountOne") ?? "1 item"
        : LocalizationService.Instance?.Format("Tags.ItemCountFormat", MediaCount) ?? $"{MediaCount} items";
}

/// <summary>
/// Tag management page: list every tag with its usage count, rename, delete, remove unused,
/// or open a tag as a filtered library view.
/// </summary>
public sealed partial class TagsViewModel : ViewModelBase
{
    private const int PageSize = 20;

    private readonly ITagService _tagService;
    private readonly IDialogService _dialogService;
    private readonly INavigationService _navigation;
    private readonly ILogger<TagsViewModel> _logger;
    private readonly ILocalizationService _loc;

    [ObservableProperty] private bool _isEmpty;
    [ObservableProperty] private string _newTagName = string.Empty;

    [ObservableProperty] private int _page = 1;
    [ObservableProperty] private int _totalPages = 1;
    [ObservableProperty] private bool _pagingVisible;

    public TagsViewModel(
        ITagService tagService,
        IDialogService dialogService,
        INavigationService navigation,
        ILocalizationService loc,
        ILogger<TagsViewModel> logger)
    {
        _tagService = tagService;
        _dialogService = dialogService;
        _navigation = navigation;
        _loc = loc;
        _logger = logger;
    }

    /// <summary>Every tag, in the user's arranged order. Drag-and-drop reorders this list.</summary>
    public ObservableCollection<TagRowViewModel> Tags { get; } = new();

    /// <summary>The current page of <see cref="Tags"/> shown on screen.</summary>
    public ObservableCollection<TagRowViewModel> VisibleTags { get; } = new();

    public string PageLabel => _loc.Format("Tags.PageFormat", Page, TotalPages);

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
            Page = 1;
            ApplyPage();
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

    /// <summary>
    /// Moves <paramref name="dragged"/> to <paramref name="target"/>'s position in the master
    /// <see cref="Tags"/> order — landing just before it when dragged backward through the list,
    /// just after it when dragged forward (the same remove-then-insert convention
    /// <see cref="System.Collections.ObjectModel.ObservableCollection{T}.Move"/> already uses,
    /// so the drop feels exactly like reordering tabs or list rows anywhere else). Both tags may
    /// be on different pages — the master list holds every tag regardless of which page is
    /// currently visible. Refreshes the visible page and persists the new order. Called from
    /// <c>TagsView</c>'s drag/drop code-behind.
    /// </summary>
    public async Task ReorderTagAsync(TagRowViewModel dragged, TagRowViewModel target)
    {
        if (dragged == target)
        {
            return;
        }

        var fromIndex = Tags.IndexOf(dragged);
        var toIndex = Tags.IndexOf(target);
        if (fromIndex < 0 || toIndex < 0)
        {
            return;
        }

        Tags.Move(fromIndex, toIndex);
        ApplyPage();

        try
        {
            await _tagService.ReorderAsync(Tags.Select(t => t.Id).ToList());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist tag order.");
            _dialogService.ShowError(_loc.Text("Tags.ReorderFailed"));
        }
    }

    [RelayCommand(CanExecute = nameof(CanPrevPage))]
    private void PrevPage()
    {
        Page--;
        ApplyPage();
    }

    [RelayCommand(CanExecute = nameof(CanNextPage))]
    private void NextPage()
    {
        Page++;
        ApplyPage();
    }

    private bool CanPrevPage() => Page > 1;

    private bool CanNextPage() => Page < TotalPages;

    private void ApplyPage()
    {
        TotalPages = Math.Max(1, (int)Math.Ceiling(Tags.Count / (double)PageSize));
        Page = Math.Clamp(Page, 1, TotalPages);
        PagingVisible = TotalPages > 1;

        VisibleTags.Clear();
        foreach (var tag in Tags.Skip((Page - 1) * PageSize).Take(PageSize))
        {
            VisibleTags.Add(tag);
        }

        OnPropertyChanged(nameof(PageLabel));
        PrevPageCommand.NotifyCanExecuteChanged();
        NextPageCommand.NotifyCanExecuteChanged();
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
            _dialogService.ShowError(_loc.Text("Tags.CreateFailed"));
        }
    }

    [RelayCommand]
    private async Task Rename(TagRowViewModel? row)
    {
        if (row is null)
        {
            return;
        }

        var input = _dialogService.Prompt(_loc.Text("Tags.RenameTitle"), _loc.Text("Tags.RenamePrompt"), row.Name);
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
            _dialogService.ShowError(ex is InvalidOperationException ? ex.Message : _loc.Text("Tags.RenameFailed"));
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
            ? _loc.Format("Tags.DeleteUsedFormat", row.Name, row.UsageLabel)
            : _loc.Format("Tags.DeleteUnusedFormat", row.Name);
        if (!_dialogService.Confirm(message, _loc.Text("Tags.DeleteTitle")))
        {
            return;
        }

        await _tagService.DeleteAsync(row.Id);
        await LoadAsync();
    }

    [RelayCommand]
    private async Task DeleteUnused()
    {
        if (!_dialogService.Confirm(_loc.Text("Tags.CleanupConfirm"), _loc.Text("Tags.CleanupTitle")))
        {
            return;
        }

        var removed = await _tagService.DeleteUnusedAsync();
        _dialogService.ShowInfo(removed == 0 ? _loc.Text("Tags.RemovedNone") : _loc.Format("Tags.RemovedFormat", removed));
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
            _loc.Format("Library.Preset.TagFormat", row.Name),
            _loc.Text("Library.Preset.TagSub"),
            TagId: row.Id)));
    }
}
