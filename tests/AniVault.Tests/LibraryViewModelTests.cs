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

    private static async Task<LibraryViewModel> CreateWithAnimeAsync(TestLibrary lib, int animeCount, LibraryLayout? layout = null)
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
            layout ?? new LibraryLayout(),
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

    [Theory]
    [InlineData(7, 35)]
    [InlineData(6, 30)]
    [InlineData(5, 35)]
    [InlineData(4, 32)]
    [InlineData(3, 33)]
    [InlineData(2, 34)]
    [InlineData(1, 35)]
    [InlineData(8, 32)]
    [InlineData(40, 35)]  // more columns than a page may hold: nothing to fill
    [InlineData(0, 35)]
    public void PageSize_Is_The_Largest_Whole_Number_Of_Rows_Within_35(int columns, int expected)
        => Assert.Equal(expected, LibraryLayout.PageSizeFor(columns));

    [Fact]
    public async Task Narrower_Window_Shrinks_The_Page_To_Whole_Rows_And_Stays_On_The_Same_Cards()
    {
        using var lib = new TestLibrary();
        var vm = await CreateWithAnimeAsync(lib, 80); // 7 per row: 35 + 35 + 10

        vm.NextPageCommand.Execute(null);
        Assert.Equal(35, vm.Items.Count);
        var firstOnScreen = vm.Items[0].Id; // item #36 of the result set

        vm.SetColumns(6); // 30 per page now: 30 + 30 + 20

        Assert.Equal(3, vm.TotalPages);
        Assert.Equal(30, vm.Items.Count);
        Assert.Equal(2, vm.CurrentPage); // items 31-60 hold #36
        Assert.Contains(vm.Items, card => card.Id == firstOnScreen);
    }

    [Fact]
    public async Task SetColumns_With_The_Same_Value_Does_Not_Rebuild_The_Page()
    {
        using var lib = new TestLibrary();
        var vm = await CreateWithAnimeAsync(lib, 40);
        var card = vm.Items[0];

        vm.SetColumns(7);

        Assert.Same(card, vm.Items[0]);
    }

    [Fact]
    public async Task The_Next_Library_Page_Starts_With_The_Row_Width_The_Last_One_Measured()
    {
        using var lib = new TestLibrary();
        var layout = new LibraryLayout();
        var first = await CreateWithAnimeAsync(lib, 40, layout);
        first.SetColumns(6);

        // A fresh page (same shared layout, as in the app) loads 30 straight away - no reload.
        var second = await CreateWithAnimeAsync(lib, 0, layout);

        Assert.Equal(30, second.Items.Count);
        Assert.Equal(2, second.TotalPages);
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
