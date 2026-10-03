using System;
using System.IO;
using Microsoft.Extensions.Logging;

namespace AniVault.Services;

/// <summary>
/// The single-file AniVault.exe cannot load its bundled native libraries straight from the exe,
/// so the .NET host unpacks them once per build into <c>%TEMP%\.net\AniVault\&lt;build id&gt;\</c>
/// and never removes them. Every update therefore leaves the previous build's copy behind.
/// This removes those leftovers, keeping only the folder the running build is using — the one
/// thing AniVault would otherwise leave on the system drive outside its own folders.
/// </summary>
public static class ExtractionCacheCleaner
{
    private const string PendingDeleteSuffix = ".delete";

    /// <summary>
    /// The folder this process's native libraries were unpacked to, or null when the app is not
    /// running as a single-file build (a dev build, the test host) — in which case nothing here
    /// is ours to clean.
    /// </summary>
    public static string? FindCurrentExtractionDirectory()
    {
        if (AppContext.GetData("NATIVE_DLL_SEARCH_DIRECTORIES") is not string searchDirectories)
        {
            return null;
        }

        foreach (var entry in searchDirectories.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var directory = Path.TrimEndingDirectorySeparator(entry);

            // <base>\.net\<app name>\<build id> — only that shape is the host's extraction folder.
            var appFolder = Path.GetDirectoryName(directory);
            var dotnetFolder = appFolder is null ? null : Path.GetDirectoryName(appFolder);
            if (dotnetFolder is not null
                && string.Equals(Path.GetFileName(dotnetFolder), ".net", StringComparison.OrdinalIgnoreCase)
                && Directory.Exists(directory))
            {
                return directory;
            }
        }

        return null;
    }

    /// <summary>
    /// Deletes every sibling of <paramref name="currentDirectory"/>. A folder some other running
    /// copy still has libraries loaded from is left completely untouched. Returns how many were removed.
    /// </summary>
    public static int RemoveOtherBuilds(string currentDirectory, ILogger logger)
    {
        var current = Path.TrimEndingDirectorySeparator(currentDirectory);
        var appFolder = Path.GetDirectoryName(current);
        if (appFolder is null || !Directory.Exists(appFolder))
        {
            return 0;
        }

        var removed = 0;
        foreach (var sibling in Directory.GetDirectories(appFolder))
        {
            if (string.Equals(sibling, current, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            try
            {
                // Renaming a folder fails as a whole while any file inside it is open, so this is
                // an all-or-nothing "is it in use?" check — a plain recursive delete would instead
                // remove whichever files happen not to be loaded yet and break that other copy.
                // (A folder already carrying the suffix is one an earlier clean-up renamed but
                // could not finish deleting.)
                var doomed = sibling;
                if (!sibling.EndsWith(PendingDeleteSuffix, StringComparison.OrdinalIgnoreCase))
                {
                    doomed = sibling + PendingDeleteSuffix;
                    Directory.Move(sibling, doomed);
                }

                Directory.Delete(doomed, recursive: true);
                removed++;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                logger.LogDebug(ex, "Left the extraction folder {Folder} alone (in use or not removable).", sibling);
            }
        }

        return removed;
    }
}
