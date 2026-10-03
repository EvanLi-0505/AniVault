namespace AniVault.ViewModels;

/// <summary>
/// How many poster cards fit on one row of the library grid right now, and the page size that
/// follows from it. One shared instance: the window is as wide for the next library page as it
/// was for the last, so a new <see cref="LibraryViewModel"/> starts with the right page size
/// instead of loading a page and then re-rendering it once the view has been measured.
/// </summary>
public sealed class LibraryLayout
{
    /// <summary>
    /// Upper bound for one page. The grid is not virtualized, so render cost grows with the card
    /// count (measured ~150-270ms for 47 cards vs ~20-45ms for 5); 35 keeps a page turn fast.
    /// </summary>
    public const int MaxPageSize = 35;

    /// <summary>Cards per row, as last reported by <c>LibraryView</c>. 7 = a maximized window.</summary>
    public int Columns { get; set; } = 7;

    public int PageSize => PageSizeFor(Columns);

    /// <summary>
    /// The largest whole number of rows that stays within <see cref="MaxPageSize"/>, so every
    /// page except the last ends on a full row: 7 per row → 35, 6 → 30, 4 → 32, 3 → 33.
    /// </summary>
    public static int PageSizeFor(int columns)
        => columns is < 1 or >= MaxPageSize ? MaxPageSize : MaxPageSize - (MaxPageSize % columns);
}
