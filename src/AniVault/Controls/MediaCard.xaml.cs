using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace AniVault.Controls;

/// <summary>
/// Poster card for a <c>MediaCardViewModel</c> (set as the DataContext). The hosting page
/// supplies the three commands; the card raises them with itself (the view model) as the parameter.
/// </summary>
public partial class MediaCard : UserControl
{
    public static readonly DependencyProperty OpenCommandProperty = DependencyProperty.Register(
        nameof(OpenCommand), typeof(ICommand), typeof(MediaCard), new PropertyMetadata(null));

    public static readonly DependencyProperty EditCommandProperty = DependencyProperty.Register(
        nameof(EditCommand), typeof(ICommand), typeof(MediaCard), new PropertyMetadata(null));

    public static readonly DependencyProperty DeleteCommandProperty = DependencyProperty.Register(
        nameof(DeleteCommand), typeof(ICommand), typeof(MediaCard), new PropertyMetadata(null));

    public MediaCard()
    {
        InitializeComponent();
    }

    public ICommand? OpenCommand
    {
        get => (ICommand?)GetValue(OpenCommandProperty);
        set => SetValue(OpenCommandProperty, value);
    }

    public ICommand? EditCommand
    {
        get => (ICommand?)GetValue(EditCommandProperty);
        set => SetValue(EditCommandProperty, value);
    }

    public ICommand? DeleteCommand
    {
        get => (ICommand?)GetValue(DeleteCommandProperty);
        set => SetValue(DeleteCommandProperty, value);
    }
}
