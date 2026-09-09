using System;
using AniVault.Models;

namespace AniVault.Utilities;

/// <summary>
/// Maps calendar months to the anime broadcast-season convention used by seasonal anime charts:
/// Winter = Dec-Feb, Spring = Mar-May, Summer = Jun-Aug, Fall = Sep-Nov.
/// (A show premiering in late September counts as that year's Fall, matching common usage.)
/// December belongs to the <i>following</i> year's Winter.
/// </summary>
public static class SeasonHelper
{
    public static AnimeSeason ForMonth(int month) => month switch
    {
        12 or 1 or 2 => AnimeSeason.Winter,
        3 or 4 or 5 => AnimeSeason.Spring,
        6 or 7 or 8 => AnimeSeason.Summer,
        9 or 10 or 11 => AnimeSeason.Fall,
        _ => throw new ArgumentOutOfRangeException(nameof(month), month, "Month must be 1-12."),
    };

    /// <summary>The broadcast year and season a date falls in (December rolls into next year's Winter).</summary>
    public static (int Year, AnimeSeason Season) ForDate(DateOnly date)
    {
        var year = date.Month == 12 ? date.Year + 1 : date.Year;
        return (year, ForMonth(date.Month));
    }

    public static (int Year, AnimeSeason Season) Current()
        => ForDate(DateOnly.FromDateTime(DateTime.Now));
}
