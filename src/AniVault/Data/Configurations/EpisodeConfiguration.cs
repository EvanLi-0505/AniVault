using AniVault.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AniVault.Data.Configurations;

public class EpisodeConfiguration : IEntityTypeConfiguration<Episode>
{
    public void Configure(EntityTypeBuilder<Episode> builder)
    {
        builder.ToTable("Episodes");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Title).HasMaxLength(500);

        // One row per episode number within a media item.
        builder.HasIndex(e => new { e.MediaId, e.EpisodeNumber }).IsUnique();
    }
}
