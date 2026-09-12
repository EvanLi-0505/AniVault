using System;
using System.Linq;
using System.Threading.Tasks;
using AniVault.Services;
using AniVault.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;

namespace AniVault.Tests;

public class TagsViewModelTests
{
    private sealed class FakeDialogService : IDialogService
    {
        public string? LastErrorMessage { get; private set; }

        public string? PickFolder(string title, string? initialDirectory = null) => null;

        public string? PickFile(string title, string filter) => null;

        public string? PickSaveFile(string title, string filter, string defaultFileName) => null;

        public void ShowInfo(string message, string title = "AniVault")
        {
        }

        public void ShowWarning(string message, string title = "AniVault")
        {
        }

        public void ShowError(string message, string title = "AniVault") => LastErrorMessage = message;

        public bool Confirm(string message, string title = "AniVault") => true;

        public string? Prompt(string title, string message, string? initialValue = null) => null;

        public DialogChoice AskThreeWay(string message, string title, string primaryLabel, string secondaryLabel) => DialogChoice.Cancel;
    }

    private sealed class FakeNavigationService : INavigationService
    {
        public ViewModelBase? CurrentViewModel => null;

        public bool CanGoBack => false;

        public event Action? CurrentViewModelChanged { add { } remove { } }

        public void NavigateTo<TViewModel>(Action<TViewModel>? configure = null) where TViewModel : ViewModelBase
        {
        }

        public void NavigateTo(Type viewModelType)
        {
        }

        public void NavigateToDetail<TViewModel>(Action<TViewModel>? configure = null) where TViewModel : ViewModelBase
        {
        }

        public void GoBack()
        {
        }
    }

    private static async Task<(TagsViewModel Vm, TagService TagService)> CreateAsync(TestDatabase db, int tagCount, string prefix = "T")
    {
        var settings = new SettingsService(db);
        var tagService = new TagService(db);
        for (var i = 1; i <= tagCount; i++)
        {
            await tagService.GetOrCreateAsync($"{prefix}{i}");
        }

        var vm = new TagsViewModel(
            tagService,
            new FakeDialogService(),
            new FakeNavigationService(),
            new LocalizationService(settings),
            NullLogger<TagsViewModel>.Instance);

        await vm.LoadAsync();
        return (vm, tagService);
    }

    [Fact]
    public async Task LoadAsync_Paginates_At_Twenty_Per_Page()
    {
        using var db = new TestDatabase();
        var (vm, _) = await CreateAsync(db, 25);

        Assert.Equal(25, vm.Tags.Count);
        Assert.Equal(20, vm.VisibleTags.Count);
        Assert.Equal(2, vm.TotalPages);
        Assert.True(vm.PagingVisible);
        Assert.Equal(1, vm.Page);
    }

    [Fact]
    public async Task Paging_Under_Twenty_Tags_Hides_The_Pager()
    {
        using var db = new TestDatabase();
        var (vm, _) = await CreateAsync(db, 5);

        Assert.Equal(5, vm.VisibleTags.Count);
        Assert.Equal(1, vm.TotalPages);
        Assert.False(vm.PagingVisible);
    }

    [Fact]
    public async Task Next_And_Prev_Page_Move_Between_Pages()
    {
        using var db = new TestDatabase();
        var (vm, _) = await CreateAsync(db, 25);

        vm.NextPageCommand.Execute(null);
        Assert.Equal(2, vm.Page);
        Assert.Equal(5, vm.VisibleTags.Count);
        Assert.Equal("T21", vm.VisibleTags[0].Name);

        vm.PrevPageCommand.Execute(null);
        Assert.Equal(1, vm.Page);
        Assert.Equal(20, vm.VisibleTags.Count);
    }

    [Fact]
    public async Task GoToPage_Jumps_To_A_Valid_Page_And_Clears_The_Input()
    {
        using var db = new TestDatabase();
        var (vm, _) = await CreateAsync(db, 25);

        vm.PageJumpText = "2";
        vm.GoToPageCommand.Execute(null);

        Assert.Equal(2, vm.Page);
        Assert.Equal(5, vm.VisibleTags.Count);
        Assert.Equal(string.Empty, vm.PageJumpText);
    }

    [Fact]
    public async Task GoToPage_Ignores_OutOfRange_Or_Garbage_Input()
    {
        using var db = new TestDatabase();
        var (vm, _) = await CreateAsync(db, 25);

        vm.PageJumpText = "99";
        vm.GoToPageCommand.Execute(null);
        Assert.Equal(1, vm.Page);

        vm.PageJumpText = "not a number";
        vm.GoToPageCommand.Execute(null);
        Assert.Equal(1, vm.Page);
    }

    [Fact]
    public async Task ReorderTagAsync_Moves_The_Tag_And_Persists_The_New_Order()
    {
        using var db = new TestDatabase();
        var (vm, tagService) = await CreateAsync(db, 3);

        // Starts as T1, T2, T3 (creation order). Drag T3 to sit before T1.
        await vm.ReorderTagAsync(vm.Tags[2], vm.Tags[0]);

        Assert.Equal(new[] { "T3", "T1", "T2" }, vm.Tags.Select(t => t.Name).ToArray());
        Assert.Equal(new[] { "T3", "T1", "T2" }, vm.VisibleTags.Select(t => t.Name).ToArray());

        var persisted = await tagService.GetAllWithUsageAsync();
        Assert.Equal(new[] { "T3", "T1", "T2" }, persisted.Select(t => t.Name).ToArray());
    }

    [Fact]
    public async Task ReorderTagAsync_Works_Across_Pages()
    {
        using var db = new TestDatabase();
        var (vm, _) = await CreateAsync(db, 25);

        // Drag the very last tag (page 2) to the very front (page 1).
        var last = vm.Tags[24];
        var first = vm.Tags[0];
        await vm.ReorderTagAsync(last, first);

        Assert.Equal("T25", vm.Tags[0].Name);
        Assert.Equal("T25", vm.VisibleTags[0].Name);
        Assert.Equal(1, vm.Page);
    }
}
