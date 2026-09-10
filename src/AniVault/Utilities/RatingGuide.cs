using System.IO;
using System.Linq;
using System.Reflection;

namespace AniVault.Utilities;

/// <summary>
/// The embedded 10-point rating rubric (`Resources/rating-guide.md`), shown on the My Rating
/// page and in the rating-calculator questionnaire. Loaded once, offline.
/// </summary>
public static class RatingGuide
{
    private static string? _cached;

    public static string Text => _cached ??= Load();

    private static string Load()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith("rating-guide.md", System.StringComparison.OrdinalIgnoreCase));
        if (resourceName is null)
        {
            return string.Empty;
        }

        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
        {
            return string.Empty;
        }

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
