using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace AniVault.Services;

/// <summary>The tiny bit of configuration that must exist before the data directory is known.</summary>
public sealed class BootstrapConfig
{
    /// <summary>Absolute path to the user-selected AniVault data directory, or null if not configured yet.</summary>
    public string? DataDirectory { get; set; }
}

public interface IBootstrapConfigService
{
    /// <summary>Full path of the config file that is (or would be) used for writing.</summary>
    string ConfigFilePath { get; }

    BootstrapConfig Load();

    void Save(BootstrapConfig config);
}

/// <summary>
/// Reads and writes the pointer to the data directory.
///
/// AniVault is a portable ("green") app, so the pointer is kept <b>next to the executable</b>
/// (<c>anivault.config.json</c>) whenever that folder is writable — this keeps the whole
/// install movable between machines. If the executable folder is read-only (e.g. Program
/// Files), it falls back to <c>%LOCALAPPDATA%\AniVault\bootstrap.json</c>.
///
/// The file contains no secrets and no library data — only the data-directory path.
/// </summary>
public sealed class BootstrapConfigService : IBootstrapConfigService
{
    private const string PortableFileName = "anivault.config.json";
    private const string FallbackFileName = "bootstrap.json";

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _portablePath;
    private readonly string _fallbackPath;

    public BootstrapConfigService()
    {
        _portablePath = Path.Combine(AppContext.BaseDirectory, PortableFileName);
        _fallbackPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AniVault",
            FallbackFileName);

        ConfigFilePath = CanWriteTo(AppContext.BaseDirectory) ? _portablePath : _fallbackPath;
    }

    public string ConfigFilePath { get; }

    /// <summary>Existing config files, portable location preferred.</summary>
    private IEnumerable<string> CandidatePaths => new[] { _portablePath, _fallbackPath };

    public BootstrapConfig Load()
    {
        foreach (var path in CandidatePaths.Where(File.Exists))
        {
            try
            {
                var json = File.ReadAllText(path);
                var config = JsonSerializer.Deserialize<BootstrapConfig>(json);
                if (config is not null)
                {
                    return config;
                }
            }
            catch (Exception)
            {
                // Try the next candidate; a corrupt file should not block startup.
            }
        }

        return new BootstrapConfig();
    }

    public void Save(BootstrapConfig config)
    {
        var json = JsonSerializer.Serialize(config, JsonOptions);
        var directory = Path.GetDirectoryName(ConfigFilePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(ConfigFilePath, json);
    }

    private static bool CanWriteTo(string directory)
    {
        try
        {
            var probe = Path.Combine(directory, $".write-test-{Guid.NewGuid():N}");
            File.WriteAllText(probe, string.Empty);
            File.Delete(probe);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
