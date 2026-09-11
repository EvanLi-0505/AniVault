using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AniVault.Metadata;
using AniVault.Models;
using AniVault.Services;
using AniVault.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;

namespace AniVault.Tests;

public class OnlineSearchViewModelTests
{
    private sealed class FakeMetadataService : IMetadataService
    {
        public List<MetadataSearchResult> SearchResults { get; } = new();

        public Dictionary<string, MediaMetadata> Details { get; } = new();

        public Task<IReadOnlyList<MetadataProviderInfo>> GetProvidersAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<MetadataProviderInfo>>(
                new[] { new MetadataProviderInfo(ExternalSource.Bangumi, "Bangumi", "desc", false, true, true) });

        public Task<ExternalSource> GetActiveProviderAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(ExternalSource.Bangumi);

        public Task SetActiveProviderAsync(ExternalSource source, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<bool> IsOnlineSearchEnabledAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(true);

        public Task<string?> GetApiKeyAsync(ExternalSource source, CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);

        public Task SetApiKeyAsync(ExternalSource source, string? apiKey, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<IReadOnlyList<MetadataSearchResult>> SearchAsync(
            ExternalSource source, string query, MediaType? preferredMediaType, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<MetadataSearchResult>>(SearchResults);

        public Task<MediaMetadata?> GetDetailsAsync(ExternalSource source, string externalId, CancellationToken cancellationToken) =>
            Task.FromResult(Details.GetValueOrDefault(externalId));
    }

    private sealed class FakeMetadataImporter : IMetadataImporter
    {
        public HashSet<string> DuplicateExternalIds { get; } = new();

        public List<MediaMetadata> Imported { get; } = new();

        private int _nextId = 1;

        public Task<DuplicateCheck> CheckDuplicateAsync(MediaMetadata metadata, CancellationToken cancellationToken = default) =>
            Task.FromResult(DuplicateExternalIds.Contains(metadata.ExternalId)
                ? new DuplicateCheck(true, 999, "dup")
                : new DuplicateCheck(false, null, null));

        public Task<int> ImportAsync(MediaMetadata metadata, MetadataImportOptions options, CancellationToken cancellationToken = default)
        {
            Imported.Add(metadata);
            return Task.FromResult(_nextId++);
        }

        public Task RefreshAsync(int mediaId, MediaMetadata metadata, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class FakeDialogService : IDialogService
    {
        public string? LastInfoMessage { get; private set; }

        public string? PickFolder(string title, string? initialDirectory = null) => null;

        public string? PickFile(string title, string filter) => null;

        public string? PickSaveFile(string title, string filter, string defaultFileName) => null;

        public void ShowInfo(string message, string title = "AniVault") => LastInfoMessage = message;

        public void ShowWarning(string message, string title = "AniVault")
        {
        }

        public void ShowError(string message, string title = "AniVault")
        {
        }

        public bool Confirm(string message, string title = "AniVault") => true;

        public string? Prompt(string title, string message, string? initialValue = null) => null;

        public DialogChoice AskThreeWay(string message, string title, string primaryLabel, string secondaryLabel) => DialogChoice.Secondary;
    }

    private static MetadataSearchResult MakeResult(string id, string title) => new()
    {
        Source = ExternalSource.Bangumi,
        ExternalId = id,
        Title = title,
        MediaType = MediaType.Anime,
    };

    private static MediaMetadata MakeMetadata(string id, string title) => new()
    {
        Source = ExternalSource.Bangumi,
        ExternalId = id,
        MediaType = MediaType.Anime,
        Title = title,
    };

    private static OnlineSearchViewModel Create(
        TestDatabase db, FakeMetadataService metadata, FakeMetadataImporter importer, FakeDialogService dialog)
    {
        var settings = new SettingsService(db);
        var loc = new LocalizationService(settings);
        return new OnlineSearchViewModel(metadata, importer, settings, dialog, loc, NullLogger<OnlineSearchViewModel>.Instance);
    }

    [Fact]
    public void ProviderChoice_ToString_Returns_DisplayName_Not_Record_Dump()
    {
        var choice = new ProviderChoice(ExternalSource.Bangumi, "Bangumi", true, "desc");

        Assert.Equal("Bangumi", choice.ToString());
    }

    [Fact]
    public async Task InitializeAsync_Defaults_Backdrop_Checkbox_To_Checked_Like_Poster()
    {
        using var db = new TestDatabase();
        await new SettingsService(db).SetAsync(SettingKeys.PosterDownloadEnabled, "true");
        var vm = Create(db, new FakeMetadataService(), new FakeMetadataImporter(), new FakeDialogService());

        await vm.InitializeAsync(null);

        Assert.True(vm.PosterDownloadAllowed);
        Assert.True(vm.DownloadPoster);
        Assert.True(vm.DownloadBackdrop);
    }

    [Fact]
    public async Task Selecting_Multiple_Results_Updates_SelectedCount_And_Button_Label()
    {
        using var db = new TestDatabase();
        var metadata = new FakeMetadataService();
        metadata.SearchResults.AddRange(new[]
        {
            MakeResult("1", "Show One"),
            MakeResult("2", "Show Two"),
            MakeResult("3", "Show Three"),
        });
        var vm = Create(db, metadata, new FakeMetadataImporter(), new FakeDialogService());
        await vm.InitializeAsync(null);
        vm.Query = "show";

        await vm.SearchCommand.ExecuteAsync(null);

        Assert.Equal(0, vm.SelectedCount);
        Assert.Equal("Add to Library", vm.AddButtonLabel);

        vm.Results[0].IsSelected = true;
        vm.Results[1].IsSelected = true;

        Assert.Equal(2, vm.SelectedCount);
        Assert.Equal("Add 2 to Library", vm.AddButtonLabel);
    }

    [Fact]
    public async Task CanImport_Is_True_With_Multiple_Selected_Even_Without_A_Loaded_Preview()
    {
        using var db = new TestDatabase();
        var metadata = new FakeMetadataService();
        metadata.SearchResults.AddRange(new[] { MakeResult("1", "Show One"), MakeResult("2", "Show Two") });
        var vm = Create(db, metadata, new FakeMetadataImporter(), new FakeDialogService());
        await vm.InitializeAsync(null);
        vm.Query = "show";
        await vm.SearchCommand.ExecuteAsync(null);

        Assert.False(vm.AddToLibraryCommand.CanExecute(null));

        vm.Results[0].IsSelected = true;
        vm.Results[1].IsSelected = true;

        Assert.Null(vm.Preview);
        Assert.True(vm.AddToLibraryCommand.CanExecute(null));
    }

    [Fact]
    public async Task Batch_Import_Imports_New_Items_Skips_Duplicates_And_Counts_Failures()
    {
        using var db = new TestDatabase();
        var metadata = new FakeMetadataService();
        metadata.SearchResults.AddRange(new[]
        {
            MakeResult("1", "New Show"),
            MakeResult("2", "Duplicate Show"),
            MakeResult("3", "Unfetchable Show"),
        });
        metadata.Details["1"] = MakeMetadata("1", "New Show");
        metadata.Details["2"] = MakeMetadata("2", "Duplicate Show");
        // "3" deliberately has no details entry, so GetDetailsAsync returns null (a failure).

        var importer = new FakeMetadataImporter();
        importer.DuplicateExternalIds.Add("2");
        var dialog = new FakeDialogService();

        var vm = Create(db, metadata, importer, dialog);
        await vm.InitializeAsync(null);
        vm.Query = "show";
        await vm.SearchCommand.ExecuteAsync(null);

        foreach (var result in vm.Results)
        {
            result.IsSelected = true;
        }

        var closed = false;
        vm.RequestClose += () => closed = true;

        await vm.AddToLibraryCommand.ExecuteAsync(null);

        Assert.Single(importer.Imported);
        Assert.Equal("New Show", importer.Imported[0].Title);
        Assert.Equal("Added 1. Skipped 1 possible duplicate(s). 1 failed.", dialog.LastInfoMessage);
        Assert.Equal(1, vm.ImportedMediaId);
        Assert.True(closed);
    }

    [Fact]
    public async Task Batch_Import_Does_Not_Close_The_Window_When_Nothing_Was_Imported()
    {
        using var db = new TestDatabase();
        var metadata = new FakeMetadataService();
        metadata.SearchResults.AddRange(new[] { MakeResult("1", "Dup A"), MakeResult("2", "Dup B") });
        metadata.Details["1"] = MakeMetadata("1", "Dup A");
        metadata.Details["2"] = MakeMetadata("2", "Dup B");

        var importer = new FakeMetadataImporter();
        importer.DuplicateExternalIds.Add("1");
        importer.DuplicateExternalIds.Add("2");
        var dialog = new FakeDialogService();

        var vm = Create(db, metadata, importer, dialog);
        await vm.InitializeAsync(null);
        vm.Query = "dup";
        await vm.SearchCommand.ExecuteAsync(null);

        foreach (var result in vm.Results)
        {
            result.IsSelected = true;
        }

        var closed = false;
        vm.RequestClose += () => closed = true;

        await vm.AddToLibraryCommand.ExecuteAsync(null);

        Assert.Empty(importer.Imported);
        Assert.Equal("Added 0. Skipped 2 possible duplicate(s). 0 failed.", dialog.LastInfoMessage);
        Assert.False(closed);
    }
}
