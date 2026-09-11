using System.Linq;
using System.Threading.Tasks;
using AniVault.Models;
using AniVault.Services;
using AniVault.Services.Artwork;
using AniVault.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;

namespace AniVault.Tests;

public class MediaEditorViewModelTests
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

    private static MediaEditorViewModel Create(TestLibrary lib)
    {
        var settings = new SettingsService(lib);
        var tagService = new TagService(lib);
        return new MediaEditorViewModel(
            new MediaService(lib),
            tagService,
            new ArtworkService(lib, lib.Paths, NullLogger<ArtworkService>.Instance),
            new FakeDialogService(),
            new LocalizationService(settings),
            new TagPickerViewModel(tagService),
            NullLogger<MediaEditorViewModel>.Instance);
    }

    [Fact]
    public void MediaTypeOptions_Offers_Every_Category()
    {
        using var lib = new TestLibrary();
        var vm = Create(lib);

        Assert.Equal(3, vm.MediaTypeOptions.Count);
        Assert.Contains(MediaType.Anime, vm.MediaTypeOptions);
        Assert.Contains(MediaType.Movie, vm.MediaTypeOptions);
        Assert.Contains(MediaType.TvSeries, vm.MediaTypeOptions);
    }

    [Fact]
    public async Task Save_Persists_A_Manually_Overridden_Category()
    {
        using var lib = new TestLibrary();
        var mediaService = new MediaService(lib);
        var vm = Create(lib);

        // Simulates importing something that got mis-categorized as a TV series (the Bangumi
        // bug the user hit) and manually correcting it to Movie before saving.
        await vm.InitializeForNewAsync(MediaType.TvSeries);
        vm.TitleText = "Project Hail Mary";
        vm.MediaType = MediaType.Movie;

        await vm.SaveCommand.ExecuteAsync(null);

        var saved = Assert.Single(await mediaService.GetAllDetailedAsync());
        Assert.Equal(MediaType.Movie, saved.MediaType);
    }

    [Fact]
    public async Task Switching_Away_From_Anime_Clears_The_Anime_Only_Season_Field()
    {
        using var lib = new TestLibrary();
        var mediaService = new MediaService(lib);
        var created = await mediaService.CreateAsync(new Media
        {
            MediaType = MediaType.Anime,
            Title = "Misfiled Movie",
            AirSeason = AnimeSeason.Fall,
        });

        var vm = Create(lib);
        await vm.InitializeForEditAsync(created.Id);
        Assert.Equal(MediaType.Anime, vm.MediaType);

        vm.MediaType = MediaType.Movie;
        await vm.SaveCommand.ExecuteAsync(null);

        var saved = await mediaService.GetByIdAsync(created.Id);
        Assert.Equal(MediaType.Movie, saved!.MediaType);
        Assert.Null(saved.AirSeason);
    }
}
