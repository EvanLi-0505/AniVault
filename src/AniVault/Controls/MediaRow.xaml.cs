using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace AniVault.Controls;

/// <summary>A titled, horizontally-scrolling strip of <see cref="MediaCard"/>s. Used on the Home page.</summary>
public partial class MediaRow : UserControl
{
    public static readonly DependencyProperty HeaderProperty = DependencyProperty.Register(
        nameof(Header), typeof(string), typeof(MediaRow), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(
        nameof(ItemsSource), typeof(IEnumerable), typeof(MediaRow), new PropertyMetadata(null));

    public static readonly DependencyProperty OpenCommandProperty = DependencyProperty.Register(
        nameof(OpenCommand), typeof(ICommand), typeof(MediaRow), new PropertyMetadata(null));

    public MediaRow()
    {
        InitializeComponent();
    }

    public string Header
    {
        get => (string)GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    public IEnumerable? ItemsSource
    {
        get => (IEnumerable?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public ICommand? OpenCommand
    {
        get => (ICommand?)GetValue(OpenCommandProperty);
        set => SetValue(OpenCommandProperty, value);
    }

    /// <summary>
    /// Hands every wheel tick straight to whatever ScrollViewer contains this row, instead of
    /// letting the row's own (horizontal-only) ScrollViewer swallow it. Without this, scrolling
    /// the page feels like it randomly stops working depending on whether the mouse happens to
    /// be over one of the horizontally-scrolling rows. Re-raising on the *parent* (not on
    /// <c>RowScroller</c> itself) means the synthetic bubble never re-enters this same
    /// ScrollViewer — it only travels further up, to the page's real vertical scroller.
    /// </summary>
    private void OnRowPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (e.Handled || sender is not FrameworkElement { Parent: UIElement parent })
        {
            return;
        }

        e.Handled = true;
        parent.RaiseEvent(new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
        {
            RoutedEvent = MouseWheelEvent,
        });
    }
}
