using System;
using System.Windows;
using System.Windows.Controls;
using AniVault.Services;

namespace AniVault.Controls;

/// <summary>Read-only 0-10 rating shown as five stars plus the exact number ("★★★★★  9.5 / 10").</summary>
public partial class RatingStars : UserControl
{
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value), typeof(double?), typeof(RatingStars),
        new PropertyMetadata(null, OnValueChanged));

    public RatingStars()
    {
        InitializeComponent();
        Render(null);
    }

    public double? Value
    {
        get => (double?)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((RatingStars)d).Render(e.NewValue as double?);

    private void Render(double? value)
    {
        if (value is not { } v || v <= 0)
        {
            StarsBlock.Text = "☆☆☆☆☆";
            NumberBlock.Text = LocalizationService.Instance?.Text("Card.NotRated") ?? "Not rated";
            return;
        }

        var filled = (int)Math.Round(Math.Clamp(v, 0, 10) / 2d, MidpointRounding.AwayFromZero);
        StarsBlock.Text = new string('★', filled) + new string('☆', 5 - filled);
        NumberBlock.Text = LocalizationService.Instance?.Format("Card.RatingFormat", v) ?? $"{v:0.0} / 10";
    }
}
