using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using AniVault.Models;
using AniVault.Services;

namespace AniVault.Utilities;

/// <summary>Converts a domain enum to its display label.</summary>
public sealed class EnumLabelConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        null => parameter as string ?? LocalizationService.Instance?.Text("Common.None") ?? "(None)",
        WatchStatus s => EnumDisplay.Label(s),
        MediaType t => EnumDisplay.Label(t),
        AnimeSeason season => EnumDisplay.Label(season),
        MediaSortField field => EnumDisplay.Label(field),
        AppTheme theme => LocalizationService.Instance?.Text($"Theme.{theme}") ?? theme.ToString(),
        AppLanguage lang => LocalizationService.Instance?.Text($"Language.{lang}") ?? lang.ToString(),
        _ => value.ToString() ?? string.Empty,
    };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Visible when the bound string is null / empty; Collapsed otherwise. Handy for "No poster" placeholders.</summary>
public sealed class NullOrEmptyToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var isEmpty = value is null || (value is string s && string.IsNullOrWhiteSpace(s));
        if (Invert)
        {
            isEmpty = !isEmpty;
        }

        return isEmpty ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Inverse of the built-in BooleanToVisibilityConverter: true =&gt; Collapsed.</summary>
public sealed class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Maps a <see cref="WatchStatus"/> to its themed accent brush (see DarkTheme.xaml).</summary>
public sealed class WatchStatusToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var key = value is WatchStatus status ? $"Brush.Status{status}" : "Brush.TextMuted";
        return Application.Current?.TryFindResource(key) as Brush ?? Brushes.Gray;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>bool → API-key status text.</summary>
public sealed class ApiKeyStatusConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var saved = value is true;
        return LocalizationService.Instance?.Text(saved ? "Settings.ApiKeySaved" : "Settings.ApiKeyNone")
            ?? (saved ? "An API key is saved (encrypted)." : "No API key saved yet.");
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Maps the "sort descending" bool to an up/down arrow glyph.</summary>
public sealed class SortDirectionGlyphConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? "▼" : "▲";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>true when the bound number is greater than zero — used to show the rating badge.</summary>
public sealed class PositiveNumberToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var positive = value switch
        {
            double d => d > 0,
            int i => i > 0,
            _ => false,
        };
        return positive ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
