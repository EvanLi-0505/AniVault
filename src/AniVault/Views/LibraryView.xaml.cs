using System;
using System.Windows;
using System.Windows.Controls;
using AniVault.ViewModels;

namespace AniVault.Views;

public partial class LibraryView : UserControl
{
    // One MediaCard (172) plus its 7 + 7 side margins.
    private const double CardSlotWidth = 186;

    public LibraryView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Fixes the grid at a whole number of card columns and tells the view model how many, so a
    /// page is always a whole number of rows (no half-empty last row before the pager).
    /// </summary>
    private void OnCardAreaSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (e.NewSize.Width <= 0)
        {
            return;
        }

        // Always leave room for the vertical scrollbar, whether or not it is showing: otherwise
        // its appearing could remove a column, which changes the page, which could hide it again.
        var usable = e.NewSize.Width - SystemParameters.VerticalScrollBarWidth;
        var columns = Math.Max(1, (int)Math.Floor(usable / CardSlotWidth));

        CardGrid.MaxWidth = columns * CardSlotWidth;
        (DataContext as LibraryViewModel)?.SetColumns(columns);
    }
}
