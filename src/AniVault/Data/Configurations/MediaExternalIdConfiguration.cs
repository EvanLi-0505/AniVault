using AniVault.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AniVault.Data.Configurations;

public class MediaExternalIdConfiguration : IEntityTypeConfiguration<MediaExternalId>
{
    public void Configure(EntityTypeBuilder<MediaExternalId> builder)
    {
        builder.ToTable("MediaExternalIds");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Source).HasConversion<int>();
        builder.Property(x => x.ExternalId).IsRequired().HasMaxLength(200);

        // The same provider id must not be imported twice.
        builder.HasIndex(x => new { x.Source, x.ExternalId }).IsUnique();
    }
}
