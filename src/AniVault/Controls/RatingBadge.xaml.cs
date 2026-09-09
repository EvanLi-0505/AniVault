using System.Windows;
using System.Windows.Controls;

namespace AniVault.Controls;

/// <summary>Shows "★ 9.5". Collapses itself when <see cref="Rating"/> is null or zero.</summary>
public partial class RatingBadge : UserControl
{
    public static readonly DependencyProperty RatingProperty = DependencyProperty.Register(
        nameof(Rating), typeof(double?), typeof(RatingBadge), new PropertyMetadata(null));

    public RatingBadge()
    {
        InitializeComponent();
    }

    public double? Rating
    {
        get => (double?)GetValue(RatingProperty);
        set => SetValue(RatingProperty, value);
    }
}
