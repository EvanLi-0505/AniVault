using System;
using Microsoft.EntityFrameworkCore;
using AniVault.Services;

namespace AniVault.Data;

/// <summary>
/// The runtime <see cref="IDbContextFactory{TContext}"/>. It reads the database path from
/// <see cref="IAppPathService"/> lazily — on the first <see cref="CreateDbContext"/> call —
/// so the app can start (and show first-run setup) before a data directory has been chosen.
/// The built options are cached and rebuilt only if the configured path changes.
/// </summary>
public sealed class RuntimeDbContextFactory : IDbContextFactory<AppDbContext>
{
    private readonly IAppPathService _paths;
    private readonly object _gate = new();

    private DbContextOptions<AppDbContext>? _options;
    private string? _optionsForPath;

    public RuntimeDbContextFactory(IAppPathService paths)
    {
        _paths = paths;
    }

    public AppDbContext CreateDbContext()
    {
        var databasePath = _paths.DatabaseFilePath; // throws only if still unconfigured

        lock (_gate)
        {
            if (_options is null || _optionsForPath != databasePath)
            {
                _options = new DbContextOptionsBuilder<AppDbContext>()
                    .UseSqlite($"Data Source={databasePath}")
                    .Options;
                _optionsForPath = databasePath;
            }

            return new AppDbContext(_options);
        }
    }
}
