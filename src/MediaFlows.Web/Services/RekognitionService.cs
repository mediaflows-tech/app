using Amazon.Rekognition;
using Amazon.Rekognition.Model;
using MediaFlows.Shared.Configuration;
using MediaFlows.Shared.Interfaces;
using MediaFlows.Shared.Models.ValueObjects;
using Microsoft.Extensions.Options;

namespace MediaFlows.Web.Services;

public class RekognitionService : IRekognitionService
{
    private readonly IAmazonRekognition _rekognition;
    private readonly RekognitionSettings _settings;

    public RekognitionService(IAmazonRekognition rekognition, IOptions<RekognitionSettings> settings)
    {
        _rekognition = rekognition;
        _settings = settings.Value;
    }

    public async Task<List<AssetTag>> DetectLabelsAsync(string s3Key, float minConfidence = 70f)
    {
        var response = await _rekognition.DetectLabelsAsync(new DetectLabelsRequest
        {
            Image = new Image
            {
                S3Object = new Amazon.Rekognition.Model.S3Object
                {
                    Bucket = _settings.BucketName,
                    Name = s3Key
                }
            },
            MinConfidence = minConfidence,
            MaxLabels = 20
        });

        return response.Labels.Select(l => new AssetTag
        {
            Name = l.Name,
            Confidence = l.Confidence ?? 0f
        }).ToList();
    }

    public async Task<ModerationResult> DetectModerationLabelsAsync(string s3Key)
    {
        var response = await _rekognition.DetectModerationLabelsAsync(new DetectModerationLabelsRequest
        {
            Image = new Image
            {
                S3Object = new Amazon.Rekognition.Model.S3Object
                {
                    Bucket = _settings.BucketName,
                    Name = s3Key
                }
            },
            MinConfidence = 60f
        });

        return new ModerationResult
        {
            IsSafe = !response.ModerationLabels.Any(),
            Labels = response.ModerationLabels.Select(l => new MediaFlows.Shared.Models.ValueObjects.ModerationLabel
            {
                Name = l.Name,
                ParentName = l.ParentName,
                Confidence = l.Confidence ?? 0f
            }).ToList(),
            ScannedAt = DateTime.UtcNow
        };
    }
}
