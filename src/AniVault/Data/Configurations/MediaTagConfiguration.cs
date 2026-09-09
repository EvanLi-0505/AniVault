using AniVault.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AniVault.Data.Configurations;

public class MediaTagConfiguration : IEntityTypeConfiguration<MediaTag>
{
    public void Configure(EntityTypeBuilder<MediaTag> builder)
    {
        builder.ToTable("MediaTags");

        builder.HasKey(mt => new { mt.MediaId, mt.TagId });

        builder.HasOne(mt => mt.Media!)
            .WithMany(m => m.MediaTags)
            .HasForeignKey(mt => mt.MediaId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(mt => mt.Tag!)
            .WithMany(t => t.MediaTags)
            .HasForeignKey(mt => mt.TagId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(mt => mt.TagId);
    }
}
