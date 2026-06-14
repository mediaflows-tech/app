using FluentAssertions;
using MediaFlows.Lambda.ContentModerator;
using MediaFlows.Shared.Models.Entities;
using MediaFlows.Shared.Models.Enums;
using RekLabel = Amazon.Rekognition.Model.Label;
using RekModerationLabel = Amazon.Rekognition.Model.ModerationLabel;

namespace MediaFlows.Lambda.Tests;

public class MediaAssetModerationTests
{
    [Fact]
    public void Apply_UnsafeContent_QuarantinesAndRecordsModerationResult()
    {
        var asset = new MediaAsset { Status = AssetStatus.Approved };
        var scannedAt = new DateTime(2026, 5, 21, 10, 0, 0, DateTimeKind.Utc);
        var moderationLabels = new List<RekModerationLabel>
        {
            new() { Name = "Explicit Nudity", ParentName = "Nudity", Confidence = 85.5f }
        };

        MediaAssetModeration.Apply(
            asset,
            isSafe: false,
            autoTags: new List<RekLabel>(),
            moderationLabels: moderationLabels,
            scannedAt: scannedAt);

        asset.Status.Should().Be(AssetStatus.Quarantined);
        asset.Metadata.Moderation.Should().NotBeNull();
        asset.Metadata.Moderation!.IsSafe.Should().BeFalse();
        asset.Metadata.Moderation.ScannedAt.Should().Be(scannedAt);
        asset.Metadata.Moderation.Labels.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new
            {
                Name = "Explicit Nudity",
                ParentName = "Nudity",
                Confidence = 85.5f
            });
    }

    [Fact]
    public void Apply_SafeContent_PreservesStatusAndMarksModerationSafe()
    {
        var asset = new MediaAsset { Status = AssetStatus.Approved };
        var scannedAt = new DateTime(2026, 5, 21, 10, 0, 0, DateTimeKind.Utc);

        MediaAssetModeration.Apply(
            asset,
            isSafe: true,
            autoTags: new List<RekLabel>(),
            moderationLabels: new List<RekModerationLabel>(),
            scannedAt: scannedAt);

        asset.Status.Should().Be(AssetStatus.Approved);
        asset.Metadata.Moderation.Should().NotBeNull();
        asset.Metadata.Moderation!.IsSafe.Should().BeTrue();
        asset.Metadata.Moderation.Labels.Should().BeEmpty();
        asset.Metadata.Moderation.ScannedAt.Should().Be(scannedAt);
    }

    [Fact]
    public void Apply_CopiesRekognitionLabelsToAutoTags()
    {
        var asset = new MediaAsset();
        var autoTags = new List<RekLabel>
        {
            new() { Name = "Landscape", Confidence = 98.5f },
            new() { Name = "Nature", Confidence = 95.0f }
        };

        MediaAssetModeration.Apply(
            asset,
            isSafe: true,
            autoTags: autoTags,
            moderationLabels: new List<RekModerationLabel>(),
            scannedAt: DateTime.UtcNow);

        asset.Metadata.AutoTags.Should().HaveCount(2);
        asset.Metadata.AutoTags[0].Name.Should().Be("Landscape");
        asset.Metadata.AutoTags[0].Confidence.Should().Be(98.5f);
        asset.Metadata.AutoTags[1].Name.Should().Be("Nature");
        asset.Metadata.AutoTags[1].Confidence.Should().Be(95.0f);
    }

    [Fact]
    public void Apply_PreservesUnrelatedMetadataFields()
    {
        var asset = new MediaAsset
        {
            Metadata = new MediaFlows.Shared.Models.ValueObjects.MediaMetadata
            {
                Width = 1920,
                Height = 1080,
                Format = "JPEG",
                Tags = new List<string> { "user-curated-1", "user-curated-2" },
                ExifTags = new List<MediaFlows.Shared.Models.ValueObjects.ExifTag>
                {
                    new() { Key = "Make", Value = "Canon" }
                }
            }
        };

        MediaAssetModeration.Apply(
            asset,
            isSafe: true,
            autoTags: new List<RekLabel>(),
            moderationLabels: new List<RekModerationLabel>(),
            scannedAt: DateTime.UtcNow);

        asset.Metadata.Width.Should().Be(1920);
        asset.Metadata.Height.Should().Be(1080);
        asset.Metadata.Format.Should().Be("JPEG");
        asset.Metadata.Tags.Should().Equal("user-curated-1", "user-curated-2");
        asset.Metadata.ExifTags.Should().ContainSingle()
            .Which.Key.Should().Be("Make");
    }
}
