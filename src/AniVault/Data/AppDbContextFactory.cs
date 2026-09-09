using System;
using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AniVault.Data;

/// <summary>
/// Used only by the EF Core command-line tools (<c>dotnet ef migrations add ...</c>).
/// At runtime the application builds its own <see cref="DbContextOptions"/> pointing at the
/// user-selected data directory; this factory just needs any valid SQLite connection string
/// so the tools can read the model.
/// </summary>
public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var designTimeDbPath = Path.Combine(Path.GetTempPath(), "anivault-designtime.db");

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={designTimeDbPath}")
            .Options;

        return new AppDbContext(options);
    }
}
