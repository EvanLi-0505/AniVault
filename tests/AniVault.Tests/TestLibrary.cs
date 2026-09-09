using System;
using System.IO;
using AniVault.Data;
using AniVault.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AniVault.Tests;

/// <summary>
/// A full throwaway library: a real data-directory layout on disk, an <see cref="AppPathService"/>
/// pointed at it, and a migrated SQLite database inside it. Used by backup/restore tests that
/// need the database to live where the path service expects it.
/// </summary>
public sealed class TestLibrary : IDisposable, IDbContextFactory<AppDbContext>
{
    private readonly DbContextOptions<AppDbContext> _options;

    public TestLibrary()
    {
        DataDirectory = Path.Combine(Path.GetTempPath(), $"anivault-lib-{Guid.NewGuid():N}");
        Paths = new AppPathService(new FakeBootstrapConfigService());
        Paths.SetDataDirectory(DataDirectory);
        Paths.EnsureDirectoryStructure();

        _options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={Paths.DatabaseFilePath}")
            .Options;

        using var db = CreateDbContext();
        db.Database.Migrate();
    }

    public string DataDirectory { get; }

    public AppPathService Paths { get; }

    public AppDbContext CreateDbContext() => new(_options);

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try
        {
            if (Directory.Exists(DataDirectory))
            {
                Directory.Delete(DataDirectory, recursive: true);
            }
        }
        catch (IOException)
        {
        }
    }
}
