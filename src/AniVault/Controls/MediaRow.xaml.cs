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
}
