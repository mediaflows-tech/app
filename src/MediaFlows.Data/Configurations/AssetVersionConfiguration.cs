using MediaFlows.Shared.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MediaFlows.Data.Configurations;

public class AssetVersionConfiguration : IEntityTypeConfiguration<AssetVersion>
{
    public void Configure(EntityTypeBuilder<AssetVersion> builder)
    {
        builder.HasKey(v => v.Id);

        builder.Property(v => v.S3Key).HasMaxLength(1024).IsRequired();
        builder.Property(v => v.ContentType).HasMaxLength(100).IsRequired();
        builder.Property(v => v.ChangeNotes).HasMaxLength(1000);
        builder.Property(v => v.UploadedById).HasMaxLength(128).IsRequired();

        builder.HasOne(v => v.Asset)
            .WithMany(a => a.Versions)
            .HasForeignKey(v => v.AssetId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(v => v.UploadedBy)
            .WithMany()
            .HasForeignKey(v => v.UploadedById)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(v => v.AssetId);
    }
}
