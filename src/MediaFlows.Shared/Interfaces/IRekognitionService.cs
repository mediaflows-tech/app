using MediaFlows.Shared.Models.ValueObjects;

namespace MediaFlows.Shared.Interfaces;

public interface IRekognitionService
{
    Task<List<AssetTag>> DetectLabelsAsync(string s3Key, float minConfidence = 70f);
    Task<ModerationResult> DetectModerationLabelsAsync(string s3Key);
}
