using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using AniVault.ViewModels;

namespace AniVault.Views;

public partial class TagsView : UserControl
{
    /// <summary>How long a dragged tag must hover over ‹ / › before the first page turn.</summary>
    private static readonly TimeSpan PagerHoverDelay = TimeSpan.FromMilliseconds(650);

    /// <summary>How often the page keeps turning while the drag keeps hovering after that.</summary>
    private static readonly TimeSpan PagerHoverRepeatDelay = TimeSpan.FromMilliseconds(350);

    /// <summary>How close to the top/bottom edge (in pixels) a drag has to be to auto-scroll.</summary>
    private const double AutoScrollEdge = 44;

    private const double AutoScrollPixelsPerTick = 22;

    private Point _dragStartPoint;
    private TagRowViewModel? _dragCandidate;
    private DispatcherTimer? _pagerHoverTimer;
    private DispatcherTimer? _autoScrollTimer;
    private double _autoScrollDelta;

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
        StopAutoScroll();

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
    // Hovering a dragged tag over the button keeps flipping pages — once after PagerHoverDelay,
    // then every PagerHoverRepeatDelay for as long as the drag stays put — so holding position
    // over › walks all the way to the last page instead of moving just one page per hover.

    private void OnPagerDragEnter(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(typeof(TagRowViewModel)) || sender is not Button button)
        {
            return;
        }

        e.Effects = DragDropEffects.Move;
        e.Handled = true;

        StopAutoScroll();
        StopPagerHoverTimer();
        _pagerHoverTimer = new DispatcherTimer { Interval = PagerHoverDelay };
        _pagerHoverTimer.Tick += (_, _) =>
        {
            _pagerHoverTimer!.Interval = PagerHoverRepeatDelay;
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
        StopAutoScroll();
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

    // ---- Auto-scroll while dragging near the top/bottom edge -----------------
    // The mouse wheel doesn't raise normal routed events during an OLE drag (DragDrop.DoDragDrop
    // captures input for its own modal loop), so scrolling the list while holding a tag needs its
    // own mechanism: nudge the ScrollViewer a little on a timer whenever the drag is hovering
    // within AutoScrollEdge pixels of the top or bottom, same idea as Explorer's drag-to-scroll.

    private void OnTagListPreviewDragOver(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(typeof(TagRowViewModel)))
        {
            return;
        }

        var position = e.GetPosition(TagListScrollViewer);
        if (position.Y < AutoScrollEdge)
        {
            StartAutoScroll(-AutoScrollPixelsPerTick);
        }
        else if (position.Y > TagListScrollViewer.ActualHeight - AutoScrollEdge)
        {
            StartAutoScroll(AutoScrollPixelsPerTick);
        }
        else
        {
            StopAutoScroll();
        }
    }

    private void OnTagListDragLeave(object sender, DragEventArgs e) => StopAutoScroll();

    private void StartAutoScroll(double delta)
    {
        _autoScrollDelta = delta;
        if (_autoScrollTimer is not null)
        {
            return;
        }

        _autoScrollTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(40) };
        _autoScrollTimer.Tick += (_, _) =>
            TagListScrollViewer.ScrollToVerticalOffset(TagListScrollViewer.VerticalOffset + _autoScrollDelta);
        _autoScrollTimer.Start();
    }

    private void StopAutoScroll()
    {
        if (_autoScrollTimer is null)
        {
            return;
        }

        _autoScrollTimer.Stop();
        _autoScrollTimer = null;
    }
}
