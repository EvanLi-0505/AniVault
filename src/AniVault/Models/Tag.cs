using System.Collections.Generic;

namespace AniVault.Models;

/// <summary>
/// A user- or provider-defined label that can be applied to many media items.
/// Users are free to create their own tags; they are not limited to provider genres.
/// </summary>
public class Tag
{
    public int Id { get; set; }

    /// <summary>Display name, e.g. "Slice of Life".</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Lower-cased, trimmed form of <see cref="Name"/>. Carries a unique index so that
    /// tags differing only by accidental casing ("Isekai" vs "isekai") are not duplicated.
    /// </summary>
    public string NormalizedName { get; set; } = string.Empty;

    public List<MediaTag> MediaTags { get; set; } = new();
}
