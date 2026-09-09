using System.Reflection;
using AniVault.Models;
using Microsoft.EntityFrameworkCore;

namespace AniVault.Data;

/// <summary>
/// The single EF Core context for the AniVault library database (one SQLite file).
/// Entity shape is configured by the <c>IEntityTypeConfiguration</c> classes in
/// <c>Data/Configurations</c>, which are picked up automatically below.
/// </summary>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<Media> Media => Set<Media>();

    public DbSet<Episode> Episodes => Set<Episode>();

    public DbSet<Tag> Tags => Set<Tag>();

    public DbSet<MediaTag> MediaTags => Set<MediaTag>();

    public DbSet<MediaExternalId> MediaExternalIds => Set<MediaExternalId>();

    public DbSet<AppSetting> AppSettings => Set<AppSetting>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }
}
