using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;

namespace AniVault.Metadata.Providers;

/// <summary>
/// A tiny dotted-path selector over <see cref="JsonElement"/>, used only by the user-defined
/// <see cref="CustomMetadataProvider"/>. Supports property access and array indices:
/// <c>data.results[0].titles.en</c>. An empty / null path returns the element unchanged.
/// It is deliberately forgiving — any miss returns null rather than throwing.
/// </summary>
public static class JsonPath
{
    public static JsonElement? Select(JsonElement root, string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return root;
        }

        var current = root;
        foreach (var segment in path.Split('.', StringSplitOptions.RemoveEmptyEntries))
        {
            var bracket = segment.IndexOf('[');
            var name = bracket >= 0 ? segment[..bracket] : segment;

            if (name.Length > 0)
            {
                if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(name, out var next))
                {
                    return null;
                }

                current = next;
            }

            while (bracket >= 0)
            {
                var close = segment.IndexOf(']', bracket);
                if (close < 0 || !int.TryParse(segment.AsSpan(bracket + 1, close - bracket - 1), out var index) || index < 0)
                {
                    return null;
                }

                if (current.ValueKind != JsonValueKind.Array || index >= current.GetArrayLength())
                {
                    return null;
                }

                var i = 0;
                JsonElement? picked = null;
                foreach (var item in current.EnumerateArray())
                {
                    if (i++ == index)
                    {
                        picked = item;
                        break;
                    }
                }

                if (picked is not { } found)
                {
                    return null;
                }

                current = found;
                bracket = segment.IndexOf('[', close);
            }
        }

        return current;
    }

    public static string? SelectString(JsonElement root, string? path)
    {
        if (Select(root, path) is not { } element)
        {
            return null;
        }

        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.ToString(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => null,
        };
    }

    public static int? SelectInt(JsonElement root, string? path)
    {
        if (Select(root, path) is not { } element)
        {
            return null;
        }

        return element.ValueKind switch
        {
            JsonValueKind.Number when element.TryGetInt32(out var i) => i,
            JsonValueKind.String when int.TryParse(element.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var i) => i,
            _ => null,
        };
    }

    public static double? SelectDouble(JsonElement root, string? path)
    {
        if (Select(root, path) is not { } element)
        {
            return null;
        }

        return element.ValueKind switch
        {
            JsonValueKind.Number when element.TryGetDouble(out var d) => d,
            JsonValueKind.String when double.TryParse(element.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var d) => d,
            _ => null,
        };
    }

    /// <summary>Enumerates the array at <paramref name="path"/>; yields nothing if it isn't an array.</summary>
    public static IEnumerable<JsonElement> SelectArray(JsonElement root, string? path)
    {
        if (Select(root, path) is { ValueKind: JsonValueKind.Array } array)
        {
            foreach (var item in array.EnumerateArray())
            {
                yield return item;
            }
        }
    }

    /// <summary>The 4-digit year found anywhere in a date-ish string ("2023", "2023-09-29").</summary>
    public static int? YearFrom(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        for (var i = 0; i + 4 <= value.Length; i++)
        {
            if (int.TryParse(value.AsSpan(i, 4), NumberStyles.None, CultureInfo.InvariantCulture, out var year)
                && year is >= 1900 and <= 2999)
            {
                return year;
            }
        }

        return null;
    }
}
