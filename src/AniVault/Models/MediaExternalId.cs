namespace AniVault.Models;

/// <summary>
/// Links a local <see cref="Media"/> record to its identifier in an external metadata source.
/// Used to detect duplicate imports and to support a manual metadata refresh later.
/// The user's local identity for a media item stays independent of these provider IDs.
/// </summary>
public class MediaExternalId
{
    public int Id { get; set; }

    public int MediaId { get; set; }

    public Media? Media { get; set; }

    public ExternalSource Source { get; set; }

    /// <summary>The identifier as used by the external source (kept as text; sources vary).</summary>
    public string ExternalId { get; set; } = string.Empty;
}
