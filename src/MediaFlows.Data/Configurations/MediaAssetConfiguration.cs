using MediaFlows.Shared.Models.Entities;
using MediaFlows.Shared.Models.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MediaFlows.Data.Configurations;

public class MediaAssetConfiguration : IEntityTypeConfiguration<MediaAsset>
{
    public void Configure(EntityTypeBuilder<MediaAsset> builder)
    {
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Title).HasMaxLength(500).IsRequired();
        builder.Property(a => a.S3Key).HasMaxLength(1024).IsRequired();
        builder.Property(a => a.ContentType).HasMaxLength(100).IsRequired();
        builder.Property(a => a.Status)
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.OwnsOne(a => a.Metadata, meta =>
        {
            meta.ToJson();
            meta.OwnsMany(m => m.AutoTags);
            meta.OwnsMany(m => m.ExifTags);
            meta.OwnsOne(m => m.Moderation, mod =>
            {
                mod.OwnsMany(r => r.Labels);
            });
        });

        builder.HasGeneratedTsVectorColumn(
            a => a.SearchVector,
            "english",
            a => new { a.Title, a.Description })
            .HasIndex(a => a.SearchVector)
            .HasMethod("GIN");

        builder.HasIndex(a => a.CreatorId);
        builder.HasIndex(a => a.Status);
        builder.HasIndex(a => a.CreatedAt);
        builder.HasIndex(a => new { a.Status, a.CreatedAt });
        builder.HasIndex(a => new { a.Status, a.ScheduledPublishAt })
            .HasFilter("\"ScheduledPublishAt\" IS NOT NULL");

        builder.HasOne(a => a.Creator)
            .WithMany()
            .HasForeignKey(a => a.CreatorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.Project)
            .WithMany(p => p.Assets)
            .HasForeignKey(a => a.ProjectId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
