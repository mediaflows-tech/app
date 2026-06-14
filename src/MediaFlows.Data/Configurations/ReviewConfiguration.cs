using MediaFlows.Shared.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MediaFlows.Data.Configurations;

public class ReviewConfiguration : IEntityTypeConfiguration<Review>
{
    public void Configure(EntityTypeBuilder<Review> builder)
    {
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Decision)
            .HasConversion<string>()
            .HasMaxLength(30);
        builder.Property(r => r.Comments).HasMaxLength(2000);
        builder.Property(r => r.ReviewerId).HasMaxLength(128).IsRequired();

        builder.HasOne(r => r.Asset)
            .WithMany(a => a.Reviews)
            .HasForeignKey(r => r.AssetId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(r => r.Reviewer)
            .WithMany()
            .HasForeignKey(r => r.ReviewerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(r => r.AssetId);
        builder.HasIndex(r => r.ReviewerId);
    }
}
