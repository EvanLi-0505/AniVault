using System;
using System.IO;
using System.Threading.Tasks;
using AniVault.Data;
using AniVault.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AniVault.Tests;

public class RuntimeDbContextFactoryTests
{
    [Fact]
    public void Can_Be_Constructed_Before_A_Data_Directory_Is_Chosen()
    {
        // This is the first-run scenario: nothing configured yet. Construction (and therefore
        // resolving FirstRunViewModel -> DatabaseInitializer -> this factory) must not throw.
        var paths = new AppPathService(new FakeBootstrapConfigService());
        Assert.False(paths.IsConfigured);

        var factory = new RuntimeDbContextFactory(paths);
        _ = new DatabaseInitializer(factory, NullLogger<DatabaseInitializer>.Instance);

        // Only actually opening a context requires configuration.
        Assert.Throws<InvalidOperationException>(() => factory.CreateDbContext());
    }

    [Fact]
    public async Task Works_Once_The_Data_Directory_Is_Set()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"anivault-rdf-{Guid.NewGuid():N}");
        try
        {
            var paths = new AppPathService(new FakeBootstrapConfigService());
            paths.SetDataDirectory(dir);
            paths.EnsureDirectoryStructure();

            var factory = new RuntimeDbContextFactory(paths);
            await new DatabaseInitializer(factory, NullLogger<DatabaseInitializer>.Instance).InitializeAsync();

            await using var db = factory.CreateDbContext();
            Assert.True(await db.Database.CanConnectAsync());
            Assert.True(File.Exists(paths.DatabaseFilePath));
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }
}
