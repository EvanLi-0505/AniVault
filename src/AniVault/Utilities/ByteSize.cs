namespace AniVault.Utilities;

/// <summary>Formats a byte count as a short human-readable size (used in maintenance summaries).</summary>
public static class ByteSize
{
    public static string Format(long bytes) => bytes switch
    {
        >= 1024 * 1024 => $"{bytes / (1024.0 * 1024.0):0.0} MB",
        >= 1024 => $"{bytes / 1024.0:0.0} KB",
        _ => $"{bytes} B",
    };
}
