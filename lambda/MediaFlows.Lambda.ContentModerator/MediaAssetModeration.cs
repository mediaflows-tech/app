using MediaFlows.Shared.Models.Entities;
using MediaFlows.Shared.Models.Enums;
using MediaFlows.Shared.Models.ValueObjects;
using RekLabel = Amazon.Rekognition.Model.Label;
using RekModerationLabel = Amazon.Rekognition.Model.ModerationLabel;

namespace MediaFlows.Lambda.ContentModerator;

public static class MediaAssetModeration
{
    public static void Apply(
        MediaAsset asset,
        bool isSafe,
        IReadOnlyList<RekLabel> autoTags,
        IReadOnlyList<RekModerationLabel> moderationLabels,
        DateTime scannedAt)
    {
        if (!isSafe)
            asset.Status = AssetStatus.Quarantined;

        asset.Metadata.AutoTags = autoTags.Select(t => new AssetTag
        {
            Name = t.Name,
            Confidence = t.Confidence.GetValueOrDefault()
        }).ToList();

        asset.Metadata.Moderation = new ModerationResult
        {
            IsSafe = isSafe,
            Labels = moderationLabels.Select(l => new ModerationLabel
            {
                Name = l.Name,
                ParentName = l.ParentName,
                Confidence = l.Confidence.GetValueOrDefault()
            }).ToList(),
            ScannedAt = scannedAt
        };
    }
}
