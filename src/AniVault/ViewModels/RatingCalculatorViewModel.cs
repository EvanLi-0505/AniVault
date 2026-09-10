using System;
using System.Globalization;
using System.Linq;
using AniVault.Services;
using AniVault.Utilities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AniVault.ViewModels;

/// <summary>
/// The rating questionnaire. The user scores five facets 0–10; the final score is
/// (sum ÷ 5) plus up to +0.5 of bonuses, capped at 10.0 — the rubric in
/// <c>Resources/rating-guide.md</c>. "Assign" hands the number back to the caller.
/// </summary>
public sealed partial class RatingCalculatorViewModel : ObservableObject
{
    private readonly ILocalizationService _loc;

    [ObservableProperty] private int _plot = 7;
    [ObservableProperty] private int _production = 7;
    [ObservableProperty] private int _characters = 7;
    [ObservableProperty] private int _music = 7;
    [ObservableProperty] private int _immersion = 7;
    [ObservableProperty] private bool _willRewatch;
    [ObservableProperty] private bool _wantsSequel;

    public RatingCalculatorViewModel(ILocalizationService loc)
    {
        _loc = loc;
        GuideText = RatingGuide.Text;
    }

    public int[] ScoreOptions { get; } = Enumerable.Range(0, 11).ToArray();

    /// <summary>Raised with the final score when the user presses "Assign".</summary>
    public event Action<double>? Assigned;

    /// <summary>Raised when the user cancels.</summary>
    public event Action? Cancelled;

    public string GuideText { get; }

    public double BaseTotal => Plot + Production + Characters + Music + Immersion;

    public double BaseScore => Math.Round(BaseTotal / 5d, 2);

    public double Bonus => (WillRewatch ? 0.25 : 0) + (WantsSequel ? 0.25 : 0);

    public double FinalScore => Math.Min(Math.Round(BaseScore + Bonus, 2), 10.0);

    public string BreakdownText => _loc.Format(
        "Rating.BreakdownFormat",
        BaseTotal.ToString("0.#", CultureInfo.InvariantCulture),
        BaseScore.ToString("0.00", CultureInfo.InvariantCulture),
        Bonus.ToString("0.00", CultureInfo.InvariantCulture));

    public string FinalText => _loc.Format("Rating.FinalFormat", FinalScore.ToString("0.0#", CultureInfo.InvariantCulture));

    [RelayCommand]
    private void Assign() => Assigned?.Invoke(FinalScore);

    [RelayCommand]
    private void Cancel() => Cancelled?.Invoke();

    partial void OnPlotChanged(int value) => Recompute();
    partial void OnProductionChanged(int value) => Recompute();
    partial void OnCharactersChanged(int value) => Recompute();
    partial void OnMusicChanged(int value) => Recompute();
    partial void OnImmersionChanged(int value) => Recompute();
    partial void OnWillRewatchChanged(bool value) => Recompute();
    partial void OnWantsSequelChanged(bool value) => Recompute();

    private void Recompute()
    {
        OnPropertyChanged(nameof(BaseTotal));
        OnPropertyChanged(nameof(BaseScore));
        OnPropertyChanged(nameof(Bonus));
        OnPropertyChanged(nameof(FinalScore));
        OnPropertyChanged(nameof(BreakdownText));
        OnPropertyChanged(nameof(FinalText));
    }
}
