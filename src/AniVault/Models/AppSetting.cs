namespace AniVault.Models;

/// <summary>
/// A single application setting stored as a key/value pair inside the library database
/// (e.g. active metadata provider, theme, whether online search is enabled).
///
/// The one setting that cannot live here is the data directory location itself — that is
/// stored in a small bootstrap file outside the data directory (see BootstrapConfigService).
/// </summary>
public class AppSetting
{
    /// <summary>Stable setting key. See <see cref="SettingKeys"/>.</summary>
    public string Key { get; set; } = string.Empty;

    public string? Value { get; set; }
}

/// <summary>Well-known <see cref="AppSetting"/> keys.</summary>
public static class SettingKeys
{
    public const string Theme = "theme";
    public const string Language = "language";
    public const string MetadataProvider = "metadata.provider";
    public const string OnlineSearchEnabled = "network.onlineSearchEnabled";
    public const string PosterDownloadEnabled = "network.posterDownloadEnabled";
    public const string SchemaNote = "schema.note";
}
