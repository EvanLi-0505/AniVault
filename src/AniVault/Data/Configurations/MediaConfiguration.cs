using AniVault.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AniVault.Data.Configurations;

/// <summary>EF Core mapping for <see cref="Media"/>, including indexes used by library filtering.</summary>
public class MediaConfiguration : IEntityTypeConfiguration<Media>
{
    public void Configure(EntityTypeBuilder<Media> builder)
    {
        builder.ToTable("Media");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.Title)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(m => m.OriginalTitle).HasMaxLength(500);
        builder.Property(m => m.AlternativeTitles).HasMaxLength(2000);
        builder.Property(m => m.Country).HasMaxLength(100);
        builder.Property(m => m.OfficialWebsite).HasMaxLength(1000);
        builder.Property(m => m.PosterPath).HasMaxLength(1000);
        builder.Property(m => m.BackdropPath).HasMaxLength(1000);

        // Enums are stored as integers (default), keeping user-facing labels out of the database.
        builder.Property(m => m.MediaType).HasConversion<int>();
        builder.Property(m => m.Status).HasConversion<int>();
        builder.Property(m => m.AirSeason).HasConversion<int?>();

        // Indexes for the most common library queries.
        builder.HasIndex(m => m.MediaType);
        builder.HasIndex(m => m.Status);
        builder.HasIndex(m => new { m.MediaType, m.AirYear, m.AirSeason });
        builder.HasIndex(m => m.IsFavorite);
        builder.HasIndex(m => m.IsLiked);
        builder.HasIndex(m => m.Title);

        builder.HasMany(m => m.Episodes)
            .WithOne(e => e.Media!)
            .HasForeignKey(e => e.MediaId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(m => m.ExternalIds)
            .WithOne(x => x.Media!)
            .HasForeignKey(x => x.MediaId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
