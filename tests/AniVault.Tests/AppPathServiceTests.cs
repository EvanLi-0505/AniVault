using System;
using System.IO;
using AniVault.Models;
using AniVault.Services;

namespace AniVault.Tests;

public class AppPathServiceTests : IDisposable
{
    private readonly string _dataDir;
    private readonly AppPathService _paths;

    public AppPathServiceTests()
    {
        _dataDir = Path.Combine(Path.GetTempPath(), $"anivault-paths-{Guid.NewGuid():N}");
        _paths = new AppPathService(new FakeBootstrapConfigService());
        _paths.SetDataDirectory(_dataDir);
        _paths.EnsureDirectoryStructure();
    }

    [Fact]
    public void EnsureDirectoryStructure_Creates_Standard_Folders()
    {
        Assert.True(Directory.Exists(_paths.DatabaseDirectory));
        Assert.True(Directory.Exists(_paths.LogsDirectory));
        Assert.True(Directory.Exists(_paths.BackupsDirectory));
        Assert.True(Directory.Exists(Path.Combine(_dataDir, "Anime")));
    }

    [Fact]
    public void Relative_And_Absolute_Paths_RoundTrip()
    {
        var absolute = Path.Combine(_paths.GetMediaAssetDirectory(MediaType.Anime, 12), "poster.jpg");

        var relative = _paths.ToRelativePath(absolute);
        Assert.Equal("Anime/12/poster.jpg", relative);

        var backToAbsolute = _paths.ToAbsolutePath(relative);
        Assert.Equal(Path.GetFullPath(absolute), backToAbsolute);
    }

    [Fact]
    public void Unconfigured_Service_Reports_Not_Configured()
    {
        var fresh = new AppPathService(new FakeBootstrapConfigService());
        Assert.False(fresh.IsConfigured);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dataDir))
        {
            Directory.Delete(_dataDir, recursive: true);
        }
    }
}
