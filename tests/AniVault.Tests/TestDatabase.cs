using System;
using System.IO;
using AniVault.Data;
using Microsoft.EntityFrameworkCore;

namespace AniVault.Tests;

/// <summary>
/// Creates a throwaway SQLite database on disk and applies the real EF Core migrations to it,
/// so tests exercise the same schema the application ships.
/// </summary>
public sealed class TestDatabase : IDisposable, IDbContextFactory<AppDbContext>
{
    private readonly string _databasePath;
    private readonly DbContextOptions<AppDbContext> _options;

    public TestDatabase()
    {
        _databasePath = Path.Combine(Path.GetTempPath(), $"anivault-test-{Guid.NewGuid():N}.db");
        _options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={_databasePath}")
            .Options;

        using var db = CreateDbContext();
        db.Database.Migrate();
    }

    public AppDbContext CreateDbContext() => new(_options);

    public void Dispose()
    {
        // Ensure all pooled connections are gone before deleting the file.
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (File.Exists(_databasePath))
        {
            try
            {
                File.Delete(_databasePath);
            }
            catch (IOException)
            {
                // Best-effort cleanup; the OS temp folder is purged periodically anyway.
            }
        }
    }
}
