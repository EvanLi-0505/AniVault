using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace AniVault.Controls;

/// <summary>A tag pill. If <see cref="RemoveCommand"/> is set, shows an "x" that raises it with the tag text.</summary>
public partial class TagChip : UserControl
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text), typeof(string), typeof(TagChip), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty RemoveCommandProperty = DependencyProperty.Register(
        nameof(RemoveCommand), typeof(ICommand), typeof(TagChip), new PropertyMetadata(null));

    public TagChip()
    {
        InitializeComponent();
    }

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public ICommand? RemoveCommand
    {
        get => (ICommand?)GetValue(RemoveCommandProperty);
        set => SetValue(RemoveCommandProperty, value);
    }
}
