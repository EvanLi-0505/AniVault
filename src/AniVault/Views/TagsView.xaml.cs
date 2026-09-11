using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using AniVault.ViewModels;

namespace AniVault.Views;

public partial class TagsView : UserControl
{
    /// <summary>How long a dragged tag must hover over ‹ / › before the page turns.</summary>
    private static readonly TimeSpan PagerHoverDelay = TimeSpan.FromMilliseconds(650);

    private Point _dragStartPoint;
    private TagRowViewModel? _dragCandidate;
    private DispatcherTimer? _pagerHoverTimer;

    public TagsView()
    {
        InitializeComponent();
    }

    /// <summary>Clicking anywhere in the icon/placeholder area focuses the actual text box.</summary>
    private void OnNewTagBoxMouseDown(object sender, MouseButtonEventArgs e)
    {
        NewTagBox.Focus();
    }

    // ---- Row drag-to-reorder -------------------------------------------------
    // Standard WPF "click vs. drag" pattern: record the press point, and only start an OLE
    // drag once the mouse has actually moved past the system drag threshold while still down.
    // Neither handler marks the event Handled, so a plain click still reaches the row's own
    // Open/Rename/Delete buttons normally.

    private void OnTagRowPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        _dragStartPoint = e.GetPosition(null);
        _dragCandidate = (sender as FrameworkElement)?.DataContext as TagRowViewModel;
    }

    private void OnTagRowPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (_dragCandidate is null || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var current = e.GetPosition(null);
        if (Math.Abs(current.X - _dragStartPoint.X) < SystemParameters.MinimumHorizontalDragDistance
            && Math.Abs(current.Y - _dragStartPoint.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        var dragged = _dragCandidate;
        _dragCandidate = null;
        DragDrop.DoDragDrop((DependencyObject)sender, new DataObject(typeof(TagRowViewModel), dragged), DragDropEffects.Move);
    }

    private void OnTagRowDragEnter(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(typeof(TagRowViewModel)))
        {
            return;
        }

        // Entering a row cancels any pending page turn from hovering over ‹ / › a moment ago.
        StopPagerHoverTimer();

        if (sender is Border border)
        {
            border.SetResourceReference(Border.BorderBrushProperty, "Brush.Primary");
            border.BorderThickness = new Thickness(2);
        }

        e.Effects = DragDropEffects.Move;
        e.Handled = true;
    }

    private void OnTagRowDragLeave(object sender, DragEventArgs e)
    {
        if (sender is Border border)
        {
            border.ClearValue(Border.BorderBrushProperty);
            border.ClearValue(Border.BorderThicknessProperty);
        }
    }

    private void OnTagRowDrop(object sender, DragEventArgs e)
    {
        if (sender is Border border)
        {
            border.ClearValue(Border.BorderBrushProperty);
            border.ClearValue(Border.BorderThicknessProperty);
        }

        if (e.Data.GetData(typeof(TagRowViewModel)) is not TagRowViewModel dragged
            || (sender as FrameworkElement)?.DataContext is not TagRowViewModel target
            || DataContext is not TagsViewModel vm)
        {
            return;
        }

        e.Handled = true;
        _ = vm.ReorderTagAsync(dragged, target);
    }

    // ---- Dragging onto ‹ / › turns the page without ending the drag ----------
    // OLE drag-drop runs its own modal loop, but it still pumps window messages (that's how
    // WM_PAINT / cursor updates happen during a drag), so a DispatcherTimer keeps ticking too.
    // Hovering a dragged tag over the button for PagerHoverDelay flips the page and lets the
    // user carry on dragging onto a row on the newly-visible page before releasing.

    private void OnPagerDragEnter(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(typeof(TagRowViewModel)) || sender is not Button button)
        {
            return;
        }

        e.Effects = DragDropEffects.Move;
        e.Handled = true;

        StopPagerHoverTimer();
        _pagerHoverTimer = new DispatcherTimer { Interval = PagerHoverDelay };
        _pagerHoverTimer.Tick += (_, _) =>
        {
            StopPagerHoverTimer();
            if (button.Command?.CanExecute(button.CommandParameter) == true)
            {
                button.Command.Execute(button.CommandParameter);
            }
        };
        _pagerHoverTimer.Start();
    }

    private void OnPagerDragLeave(object sender, DragEventArgs e) => StopPagerHoverTimer();

    private void OnPagerDrop(object sender, DragEventArgs e)
    {
        // Paging happens on hover, not on drop — the Prev/Next button itself is never a
        // reorder target, so a drop landing here needs no further action.
        StopPagerHoverTimer();
        e.Handled = true;
    }

    private void StopPagerHoverTimer()
    {
        if (_pagerHoverTimer is null)
        {
            return;
        }

        _pagerHoverTimer.Stop();
        _pagerHoverTimer = null;
    }
}
