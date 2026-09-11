using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AniVault.Models;
using AniVault.Services;
using AniVault.Services.Artwork;
using AniVault.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AniVault.Tests;

public class CompressArtworkViewModelTests
{
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

        public DialogChoice AskThreeWay(string message, string title, string primaryLabel, string secondaryLabel) => DialogChoice.Cancel;
    }

    private static async Task<int> SeedOversizedPosterAsync(TestLibrary lib, string title)
    {
        await using var db = lib.CreateDbContext();
        var media = new Media { MediaType = MediaType.Anime, Title = title };
        db.Media.Add(media);
        await db.SaveChangesAsync();

        var assetDir = lib.Paths.GetMediaAssetDirectory(MediaType.Anime, media.Id);
        Directory.CreateDirectory(assetDir);
        var absolute = Path.Combine(assetDir, "poster.jpg");

        var pixels = new byte[2400 * 3200 * 4];
        var bitmap = BitmapSource.Create(2400, 3200, 96, 96, PixelFormats.Bgra32, null, pixels, 2400 * 4);
        var encoder = new JpegBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using (var stream = File.Create(absolute))
        {
            encoder.Save(stream);
        }

        media.PosterPath = lib.Paths.ToRelativePath(absolute);
        await db.SaveChangesAsync();
        return media.Id;
    }

    private static CompressArtworkViewModel Create(TestLibrary lib, FakeDialogService dialog)
    {
        var settings = new SettingsService(lib);
        var artwork = new ArtworkService(lib, lib.Paths, NullLogger<ArtworkService>.Instance);
        return new CompressArtworkViewModel(
            artwork, dialog, new LocalizationService(settings), NullLogger<CompressArtworkViewModel>.Instance);
    }

    [Fact]
    public async Task LoadAsync_Lists_Every_Oversized_Item_Preselected()
    {
        using var lib = new TestLibrary();
        var idA = await SeedOversizedPosterAsync(lib, "A");
        var idB = await SeedOversizedPosterAsync(lib, "B");

        var vm = Create(lib, new FakeDialogService());
        await vm.LoadAsync();

        Assert.Equal(2, vm.Items.Count);
        Assert.All(vm.Items, i => Assert.True(i.IsSelected));
        Assert.Equal(2, vm.SelectedCount);
        Assert.Contains(vm.Items, i => i.Item.MediaId == idA);
        Assert.Contains(vm.Items, i => i.Item.MediaId == idB);
    }

    [Fact]
    public async Task SelectNone_Then_SelectAll_Toggles_Every_Row()
    {
        using var lib = new TestLibrary();
        await SeedOversizedPosterAsync(lib, "A");
        await SeedOversizedPosterAsync(lib, "B");

        var vm = Create(lib, new FakeDialogService());
        await vm.LoadAsync();

        vm.SelectNoneCommand.Execute(null);
        Assert.Equal(0, vm.SelectedCount);
        Assert.False(vm.CompressCommand.CanExecute(null));

        vm.SelectAllCommand.Execute(null);
        Assert.Equal(2, vm.SelectedCount);
        Assert.True(vm.CompressCommand.CanExecute(null));
    }

    [Fact]
    public async Task Compress_Only_Shrinks_Rows_Left_Checked()
    {
        using var lib = new TestLibrary();
        var keepId = await SeedOversizedPosterAsync(lib, "Keep Original");
        var shrinkId = await SeedOversizedPosterAsync(lib, "Shrink Me");

        var dialog = new FakeDialogService();
        var vm = Create(lib, dialog);
        await vm.LoadAsync();

        // Uncheck the row for keepId so it's excluded from the batch.
        vm.Items.Single(i => i.Item.MediaId == keepId).IsSelected = false;

        var closed = false;
        vm.RequestClose += () => closed = true;

        await vm.CompressCommand.ExecuteAsync(null);

        Assert.True(closed);
        Assert.NotNull(dialog.LastInfoMessage);

        await using var db = lib.CreateDbContext();
        var kept = await db.Media.SingleAsync(m => m.Id == keepId);
        var shrunk = await db.Media.SingleAsync(m => m.Id == shrinkId);

        Assert.Equal(2400, ReadPixelWidth(lib.Paths.ToAbsolutePath(kept.PosterPath!)));
        Assert.True(ReadPixelWidth(lib.Paths.ToAbsolutePath(shrunk.PosterPath!)) <= 900);
    }

    private static int ReadPixelWidth(string path)
    {
        using var stream = File.OpenRead(path);
        var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);
        return decoder.Frames[0].PixelWidth;
    }
}
