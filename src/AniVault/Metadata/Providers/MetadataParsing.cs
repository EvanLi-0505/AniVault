using System;
using System.Text.Json;
using System.Text.RegularExpressions;
using AniVault.Models;
using AniVault.Utilities;

namespace AniVault.Metadata.Providers;

/// <summary>Small parsing helpers shared by the provider implementations.</summary>
internal static partial class MetadataParsing
{
    [GeneratedRegex("<[^>]+>")]
    private static partial Regex HtmlTagRegex();

    public static string? CleanText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var text = HtmlTagRegex().Replace(value, string.Empty)
            .Replace("&nbsp;", " ")
            .Replace("&amp;", "&")
            .Replace("&quot;", "\"")
            .Replace("&#39;", "'")
            .Replace("\r\n", "\n")
            .Trim();

        return text.Length == 0 ? null : text;
    }

    public static string? GetString(this JsonElement element, string property)
        => element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    public static int? GetInt(this JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetInt32(out var i) => i,
            JsonValueKind.String when int.TryParse(value.GetString(), out var i) => i,
            _ => null,
        };
    }

    public static double? GetDouble(this JsonElement element, string property)
        => element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Number
           && value.TryGetDouble(out var d)
            ? d
            : null;

    /// <summary>Parses "2023-09-29" or "2023/09/29" style dates; returns null on anything else.</summary>
    public static DateOnly? ParseDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return DateOnly.TryParse(value.Replace('/', '-'), out var date) ? date : null;
    }

    public static (int? Year, AnimeSeason? Season) YearAndSeason(DateOnly? start)
    {
        if (start is not { } d)
        {
            return (null, null);
        }

        var (year, season) = SeasonHelper.ForDate(d);
        return (year, season);
    }

    public static AnimeSeason? ParseAnimeSeason(string? value) => value?.ToUpperInvariant() switch
    {
        "WINTER" => AnimeSeason.Winter,
        "SPRING" => AnimeSeason.Spring,
        "SUMMER" => AnimeSeason.Summer,
        "FALL" or "AUTUMN" => AnimeSeason.Fall,
        _ => null,
    };
}
