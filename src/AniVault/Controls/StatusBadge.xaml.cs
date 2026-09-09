using System.Windows;
using System.Windows.Controls;
using AniVault.Models;

namespace AniVault.Controls;

/// <summary>Small coloured pill showing a <see cref="WatchStatus"/>. Reusable across cards and detail pages.</summary>
public partial class StatusBadge : UserControl
{
    public static readonly DependencyProperty StatusProperty = DependencyProperty.Register(
        nameof(Status), typeof(WatchStatus), typeof(StatusBadge),
        new PropertyMetadata(WatchStatus.Planned));

    public StatusBadge()
    {
        InitializeComponent();
    }

    public WatchStatus Status
    {
        get => (WatchStatus)GetValue(StatusProperty);
        set => SetValue(StatusProperty, value);
    }
}
