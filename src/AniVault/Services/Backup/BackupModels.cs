using System;

namespace AniVault.Services.Backup;

/// <summary>
/// Small JSON document stored inside every backup archive as <c>backup-manifest.json</c>.
/// Lets the restore step sanity-check an archive before touching the live library.
/// </summary>
public sealed class BackupManifest
{
    /// <summary>Manifest schema version, bumped only if the archive layout changes.</summary>
    public int FormatVersion { get; set; } = 1;

    public string AppVersion { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public int MediaCount { get; set; }

    public int TagCount { get; set; }

    /// <summary>Marker so a stray zip is not mistaken for an AniVault backup.</summary>
    public string Signature { get; set; } = "AniVault.Backup";
}

/// <summary>Result of inspecting a backup archive without applying it.</summary>
public sealed record BackupInspection(
    bool IsValid,
    string? Error,
    BackupManifest? Manifest,
    string FileName);
