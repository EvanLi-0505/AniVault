using System.Threading.Tasks;
using AniVault.Models;
using AniVault.Services;
using AniVault.ViewModels;

namespace AniVault.Tests;

public class FilterPanelViewModelTests
{
    private static FilterPanelViewModel Create(TestDatabase db)
    {
        var settings = new SettingsService(db);
        return new FilterPanelViewModel(new MediaQueryService(db), new TagService(db), settings, new LocalizationService(settings));
    }

    [Fact]
    public async Task Year_Can_Be_Set_To_A_Value_And_Back_To_All()
    {
        using var db = new TestDatabase();
        await using (var ctx = db.CreateDbContext())
        {
            ctx.Media.Add(new Media { MediaType = MediaType.Anime, Title = "A", AirYear = 2021 });
            await ctx.SaveChangesAsync();
        }

        var vm = Create(db);
        await vm.LoadOptionsAsync(MediaType.Anime);

        var changes = 0;
        vm.Changed += (_, _) => changes++;

        // pick 2021 via the wrapper choice, as the ComboBox binding would
        vm.YearChoice = System.Linq.Enumerable.First(vm.YearOptions, c => c.Value == 2021);
        Assert.Equal(2021, vm.Year);
        Assert.Equal(2021, vm.BuildFilter(MediaType.Anime).Year);

        // now pick "All" again — this is the case that used to be stuck
        vm.YearChoice = vm.YearOptions[0];
        Assert.Null(vm.Year);
        Assert.Null(vm.BuildFilter(MediaType.Anime).Year);
        Assert.True(changes >= 2);
    }

    [Fact]
    public async Task Month_Has_Quarterly_Presets_And_Accepts_Free_Typed_Text()
    {
        using var db = new TestDatabase();
        var vm = Create(db);
        await vm.LoadOptionsAsync(null);

        // presets: All + 1/4/7/10
        Assert.Equal(5, vm.MonthOptions.Count);
        Assert.Null(vm.Month);

        // picking a preset (ComboBox sets Text to the item string when editable)
        vm.MonthText = vm.MonthOptions[2];   // "4..."
        Assert.Equal(4, vm.Month);

        // typing an arbitrary month the presets don't cover
        vm.MonthText = "3";
        Assert.Equal(3, vm.Month);

        // garbage / out-of-range text just means "no filter"
        vm.MonthText = "13";
        Assert.Null(vm.Month);
        vm.MonthText = "abc";
        Assert.Null(vm.Month);

        // back to "All"
        vm.MonthText = vm.MonthOptions[0];
        Assert.Null(vm.Month);
        Assert.False(vm.HasActiveFilters);
    }

    [Fact]
    public async Task Status_And_Season_Round_Trip_Through_Null()
    {
        using var db = new TestDatabase();
        var vm = Create(db);
        await vm.LoadOptionsAsync(null);

        vm.Status = WatchStatus.Watching;
        vm.Season = AnimeSeason.Spring;
        Assert.Equal(WatchStatus.Watching, vm.Status);
        Assert.Equal(AnimeSeason.Spring, vm.Season);

        vm.StatusChoice = vm.StatusOptions[0];
        vm.SeasonChoice = vm.SeasonOptions[0];
        Assert.Null(vm.Status);
        Assert.Null(vm.Season);
        Assert.False(vm.HasActiveFilters);
    }

    [Fact]
    public async Task Reset_Restores_The_All_Choices()
    {
        using var db = new TestDatabase();
        var vm = Create(db);
        await vm.LoadOptionsAsync(null);

        vm.Status = WatchStatus.Dropped;
        vm.MinRating = 8;
        Assert.True(vm.HasActiveFilters);

        vm.Reset();
        Assert.Null(vm.Status);
        Assert.Null(vm.MinRating);
        Assert.Same(vm.StatusOptions[0], vm.StatusChoice);
        Assert.False(vm.HasActiveFilters);
    }

    [Fact]
    public async Task EndUpdate_Fires_Changed_But_EndUpdateSilently_Does_Not()
    {
        using var db = new TestDatabase();
        var vm = Create(db);
        await vm.LoadOptionsAsync(null);

        var changes = 0;
        vm.Changed += (_, _) => changes++;

        vm.BeginUpdate();
        vm.Status = WatchStatus.Watching;
        vm.EndUpdateSilently();
        Assert.Equal(0, changes);
        Assert.Equal(WatchStatus.Watching, vm.Status);

        vm.BeginUpdate();
        vm.Status = WatchStatus.Completed;
        vm.EndUpdate();
        Assert.Equal(1, changes);
        Assert.Equal(WatchStatus.Completed, vm.Status);
    }

    [Fact]
    public async Task Collapsing_Is_Remembered_By_The_Next_Panel_And_Is_Not_A_Filter_Change()
    {
        using var db = new TestDatabase();
        var vm = Create(db);
        await vm.LoadOptionsAsync(null);
        Assert.False(vm.IsCollapsed);

        vm.Status = WatchStatus.Watching;
        var changes = 0;
        vm.Changed += (_, _) => changes++;

        await vm.ToggleCollapsedCommand.ExecuteAsync(null);

        Assert.True(vm.IsCollapsed);
        Assert.Equal(0, changes);
        Assert.Equal(WatchStatus.Watching, vm.BuildFilter(null).Status);

        // Every browse page builds its own panel; they all share the one remembered setting.
        var next = Create(db);
        await next.LoadOptionsAsync(MediaType.Anime);
        Assert.True(next.IsCollapsed);
    }

    [Fact]
    public async Task Tags_Paginate_When_There_Are_Many()
    {
        using var db = new TestDatabase();
        await using (var ctx = db.CreateDbContext())
        {
            for (var i = 0; i < 70; i++)
            {
                ctx.Tags.Add(new Tag { Name = $"tag{i:00}", NormalizedName = $"tag{i:00}" });
            }

            await ctx.SaveChangesAsync();
        }

        var vm = Create(db);
        await vm.LoadOptionsAsync(null);

        Assert.True(vm.TagPagingVisible);
        Assert.Equal(3, vm.TagTotalPages);          // 70 / 30 -> 3 pages
        Assert.Equal(30, vm.VisibleTags.Count);

        vm.NextTagPageCommand.Execute(null);
        Assert.Equal(2, vm.TagPage);
        vm.NextTagPageCommand.Execute(null);
        Assert.Equal(10, vm.VisibleTags.Count);      // last page remainder
    }
}
