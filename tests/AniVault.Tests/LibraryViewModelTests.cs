using System;
using System.Threading.Tasks;
using AniVault.Models;
using AniVault.Services;
using AniVault.Services.Artwork;
using AniVault.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;

namespace AniVault.Tests;

public class LibraryViewModelTests
{
    private sealed class FakeMediaEditorService : IMediaEditorService
    {
        public Task<bool> AddNewAsync(MediaType mediaType) => Task.FromResult(false);

        public Task<bool> EditAsync(int mediaId) => Task.FromResult(false);
    }

    private sealed class FakeOnlineSearchService : IOnlineSearchService
    {
        public Task<int?> SearchAndImportAsync(MediaType? preferredMediaType) => Task.FromResult<int?>(null);
    }

    private sealed class FakeRatingGuideService : IRatingGuideService
    {
        public void Show()
        {
        }
    }

    private sealed class FakeDialogService : IDialogService
    {
        public string? PickFolder(string title, string? initialDirectory = null) => null;

        public string? PickFile(string title, string filter) => null;

        public string? PickSaveFile(string title, string filter, string defaultFileName) => null;

        public void ShowInfo(string message, string title = "AniVault")
        {
        }

        public void ShowWarning(string message, string title = "AniVault")
        {
        }

        public void ShowError(string message, string title = "AniVault")
        {
        }

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

    private static async Task<LibraryViewModel> CreateWithAnimeAsync(TestLibrary lib, int animeCount)
    {
        var mediaService = new MediaService(lib);
        for (var i = 0; i < animeCount; i++)
        {
            await mediaService.CreateAsync(new Media { MediaType = MediaType.Anime, Title = $"Anime {i:000}" });
        }

        var settings = new SettingsService(lib);
        var loc = new LocalizationService(settings);
        var tagService = new TagService(lib);
        var filters = new FilterPanelViewModel(new MediaQueryService(lib), tagService, settings, loc);
        var artwork = new ArtworkService(lib, lib.Paths, NullLogger<ArtworkService>.Instance);

        var vm = new LibraryViewModel(
            new MediaQueryService(lib),
            mediaService,
            new FakeMediaEditorService(),
            new FakeOnlineSearchService(),
            new FakeRatingGuideService(),
            settings,
            new FakeDialogService(),
            new MediaCardFactory(artwork),
            artwork,
            new FakeNavigationService(),
            loc,
            filters,
            NullLogger<LibraryViewModel>.Instance);

        vm.SetMediaType(MediaType.Anime);
        await vm.LoadAsync();
        return vm;
    }

    [Fact]
    public async Task Library_With_More_Items_Than_A_Page_Splits_Into_Pages()
    {
        using var lib = new TestLibrary();
        var vm = await CreateWithAnimeAsync(lib, 40); // PageSize is 35, so this is 2 pages: 35 + 5

        Assert.Equal(2, vm.TotalPages);
        Assert.True(vm.PagingVisible);
        Assert.Equal(1, vm.CurrentPage);
        Assert.Equal(35, vm.Items.Count);
    }

    [Fact]
    public async Task GoToPage_Jumps_To_A_Valid_Page_And_Clears_The_Input()
    {
        using var lib = new TestLibrary();
        var vm = await CreateWithAnimeAsync(lib, 40);

        vm.PageJumpText = "2";
        vm.GoToPageCommand.Execute(null);

        Assert.Equal(2, vm.CurrentPage);
        Assert.Equal(5, vm.Items.Count);
        Assert.Equal(string.Empty, vm.PageJumpText);
    }

    [Fact]
    public async Task GoToPage_Ignores_OutOfRange_Or_Garbage_Input()
    {
        using var lib = new TestLibrary();
        var vm = await CreateWithAnimeAsync(lib, 40);

        vm.PageJumpText = "99";
        vm.GoToPageCommand.Execute(null);
        Assert.Equal(1, vm.CurrentPage);

        vm.PageJumpText = "abc";
        vm.GoToPageCommand.Execute(null);
        Assert.Equal(1, vm.CurrentPage);

        vm.PageJumpText = "0";
        vm.GoToPageCommand.Execute(null);
        Assert.Equal(1, vm.CurrentPage);
    }

    [Fact]
    public async Task LoadAsync_Preserves_CurrentPage_For_A_GoBack_Style_Resume()
    {
        using var lib = new TestLibrary();
        var vm = await CreateWithAnimeAsync(lib, 40);

        vm.NextPageCommand.Execute(null);
        Assert.Equal(2, vm.CurrentPage);

        // NavigationService.GoBack() resumes the SAME instance by calling LoadAsync() again,
        // without re-running Configure() — the user should land back on the page they left.
        await vm.LoadAsync();

        Assert.Equal(2, vm.CurrentPage);
    }

    [Fact]
    public async Task Changing_A_Filter_Resets_Back_To_Page_One()
    {
        using var lib = new TestLibrary();
        var vm = await CreateWithAnimeAsync(lib, 40);

        vm.NextPageCommand.Execute(null);
        Assert.Equal(2, vm.CurrentPage);

        vm.Filters.Text = "Anime 000"; // fires Filters.Changed -> a debounced reload that resets the page
        await Task.Delay(500);

        Assert.Equal(1, vm.CurrentPage);
        Assert.Single(vm.Items);
    }
}
