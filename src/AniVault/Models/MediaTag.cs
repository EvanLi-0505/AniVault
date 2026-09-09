namespace AniVault.Models;

/// <summary>
/// Join entity for the many-to-many relationship between <see cref="Media"/> and <see cref="Tag"/>.
/// The pair (MediaId, TagId) is the composite primary key.
/// </summary>
public class MediaTag
{
    public int MediaId { get; set; }

    public Media? Media { get; set; }

    public int TagId { get; set; }

    public Tag? Tag { get; set; }
}
