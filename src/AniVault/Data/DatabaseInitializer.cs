using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AniVault.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AniVault.Data;

/// <summary>
/// Brings the SQLite database up to date on startup by applying any pending
/// EF Core migrations. Existing user data is never dropped.
/// </summary>
public sealed class DatabaseInitializer
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    private readonly ILogger<DatabaseInitializer> _logger;

    public DatabaseInitializer(
        IDbContextFactory<AppDbContext> contextFactory,
        ILogger<DatabaseInitializer> logger)
    {
        _contextFactory = contextFactory;
        _logger = logger;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var pending = (await db.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
        if (pending.Count > 0)
        {
            _logger.LogInformation("Applying {Count} database migration(s): {Migrations}",
                pending.Count, string.Join(", ", pending));
        }

        await db.Database.MigrateAsync(cancellationToken);

        await SeedDefaultTagsAsync(db, cancellationToken);

        _logger.LogInformation("Database ready at schema version {Version}.",
            (await db.Database.GetAppliedMigrationsAsync(cancellationToken)).LastOrDefault() ?? "(none)");
    }

    /// <summary>Adds a small starter set of common genre tags on a brand-new database only.</summary>
    private static async Task SeedDefaultTagsAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        if (await db.Tags.AnyAsync(cancellationToken))
        {
            return;
        }

        string[] starterTags =
        {
            "Action", "Adventure", "Comedy", "Drama", "Fantasy",
            "Romance", "Sci-Fi", "Slice of Life", "Sports", "Isekai",
        };

        db.Tags.AddRange(starterTags.Select(name => new Tag
        {
            Name = name,
            NormalizedName = name.Trim().ToLowerInvariant(),
        }));

        await db.SaveChangesAsync(cancellationToken);
    }
}
