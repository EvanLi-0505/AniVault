using AniVault.Services;
using AniVault.ViewModels;

namespace AniVault.Tests;

public class RatingCalculatorViewModelTests
{
    private static RatingCalculatorViewModel Create()
    {
        using var db = new TestDatabase();
        return new RatingCalculatorViewModel(new LocalizationService(new SettingsService(db)));
    }

    [Fact]
    public void Matches_The_Worked_Example_From_The_Rubric()
    {
        var vm = Create();
        vm.Plot = 8;
        vm.Production = 9;
        vm.Characters = 7;
        vm.Music = 8;
        vm.Immersion = 10;

        Assert.Equal(42, vm.BaseTotal);
        Assert.Equal(8.4, vm.BaseScore);

        vm.WillRewatch = true;
        vm.WantsSequel = true;

        Assert.Equal(0.5, vm.Bonus);
        Assert.Equal(8.9, vm.FinalScore);
    }

    [Fact]
    public void Final_Score_Is_Capped_At_Ten()
    {
        var vm = Create();
        vm.Plot = vm.Production = vm.Characters = vm.Music = vm.Immersion = 10;
        vm.WillRewatch = true;
        vm.WantsSequel = true;

        Assert.Equal(10.5, vm.BaseScore + vm.Bonus);
        Assert.Equal(10.0, vm.FinalScore);
    }

    [Fact]
    public void Assign_Raises_With_The_Final_Score()
    {
        var vm = Create();
        vm.Plot = vm.Production = vm.Characters = vm.Music = vm.Immersion = 6;

        double? assigned = null;
        vm.Assigned += s => assigned = s;
        vm.AssignCommand.Execute(null);

        Assert.Equal(6.0, assigned);
    }

    [Fact]
    public void Loads_The_Rating_Guide_Text()
    {
        Assert.Contains("剧情", Create().GuideText);
    }
}
