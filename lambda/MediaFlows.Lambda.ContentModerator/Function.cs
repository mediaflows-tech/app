using Amazon.Lambda.Core;
using Amazon.Lambda.S3Events;
using Amazon.Lambda.SQSEvents;
using Amazon.Rekognition;
using Amazon.Rekognition.Model;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Amazon.XRay.Recorder.Handlers.AwsSdk;
using MediaFlows.Data;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace MediaFlows.Lambda.ContentModerator;

public class Function
{
    private readonly IAmazonRekognition _rekognition;
    private readonly IAmazonS3 _s3Client;
    private readonly IAmazonSimpleNotificationService _sns;
    private readonly string _snsTopicArn;
    private readonly string _bucketName;
    private readonly DbContextOptions<ApplicationDbContext>? _dbOptions;
    private readonly ApplicationDbContext? _injectedDb;

    private static readonly JsonSerializerOptions CamelCase = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static readonly JsonSerializerOptions CaseInsensitive = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly HashSet<string> RekognitionImageContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png"
    };

    static Function()
    {
        AWSSDKHandler.RegisterXRayForAllServices();
    }

    public Function()
    {
        _rekognition = new AmazonRekognitionClient();
        _s3Client = new AmazonS3Client();
        _sns = new AmazonSimpleNotificationServiceClient();
        _snsTopicArn = Environment.GetEnvironmentVariable("CONTENT_FLAGGED_TOPIC_ARN")
            ?? throw new InvalidOperationException("CONTENT_FLAGGED_TOPIC_ARN not set");
        _bucketName = Environment.GetEnvironmentVariable("BUCKET_NAME")
            ?? throw new InvalidOperationException("BUCKET_NAME not set");

        var connectionString = Environment.GetEnvironmentVariable("DB_CONNECTION_STRING");
        if (!string.IsNullOrEmpty(connectionString))
        {
            _dbOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseNpgsql(connectionString)
                .Options;
        }
    }

    public Function(
        IAmazonRekognition rekognition,
        IAmazonS3 s3Client,
        IAmazonSimpleNotificationService sns,
        string snsTopicArn,
        string bucketName,
        ApplicationDbContext? db = null)
    {
        _rekognition = rekognition;
        _s3Client = s3Client;
        _sns = sns;
        _snsTopicArn = snsTopicArn;
        _bucketName = bucketName;
        _injectedDb = db;
    }

    public async Task FunctionHandler(SQSEvent sqsEvent, ILambdaContext context)
    {
        foreach (var sqsRecord in sqsEvent.Records)
        {
            var s3Event = JsonSerializer.Deserialize<S3Event>(sqsRecord.Body, CaseInsensitive);
            if (s3Event?.Records == null) continue;

            foreach (var record in s3Event.Records)
            {
                await ProcessRecord(record, context);
            }
        }
    }

    private async Task ProcessRecord(S3Event.S3EventNotificationRecord record, ILambdaContext context)
    {
        var key = Uri.UnescapeDataString(record.S3.Object.Key.Replace("+", " "));
        var bucket = record.S3.Bucket.Name;

        context.Logger.LogInformation($"Moderating: {bucket}/{key}");

        var metadata = await _s3Client.GetObjectMetadataAsync(bucket, key);
        if (!RekognitionImageContentTypes.Contains(metadata.Headers.ContentType))
        {
            context.Logger.LogInformation(
                $"Skipping Rekognition for unsupported content type {metadata.Headers.ContentType}: {bucket}/{key}");
            return;
        }

        var labelResponse = await _rekognition.DetectLabelsAsync(new DetectLabelsRequest
        {
            Image = new Image
            {
                S3Object = new Amazon.Rekognition.Model.S3Object
                {
                    Bucket = bucket,
                    Name = key
                }
            },
            MinConfidence = 75F,
            MaxLabels = 20
        });

        var autoTags = labelResponse.Labels.Select(l => new
        {
            Name = l.Name,
            Confidence = l.Confidence
        }).ToList();

        context.Logger.LogInformation($"Detected {autoTags.Count} labels for {key}");

        var moderationResponse = await _rekognition.DetectModerationLabelsAsync(
            new DetectModerationLabelsRequest
            {
                Image = new Image
                {
                    S3Object = new Amazon.Rekognition.Model.S3Object
                    {
                        Bucket = bucket,
                        Name = key
                    }
                },
                MinConfidence = 60F
            });

        bool isSafe = !moderationResponse.ModerationLabels.Any();

        if (!isSafe)
        {
            context.Logger.LogWarning($"Content flagged: {key}");

            var quarantineKey = $"quarantine/{Path.GetFileName(key)}";
            await _s3Client.CopyObjectAsync(bucket, key, _bucketName, quarantineKey);
            await _s3Client.DeleteObjectAsync(bucket, key);

            context.Logger.LogInformation($"Moved to quarantine: {quarantineKey}");

            await _sns.PublishAsync(new PublishRequest
            {
                TopicArn = _snsTopicArn,
                Subject = "Content Flagged - MediaFlows",
                Message = JsonSerializer.Serialize(new
                {
                    AssetKey = key,
                    QuarantineKey = quarantineKey,
                    Bucket = bucket,
                    ModerationLabels = moderationResponse.ModerationLabels.Select(l => new
                    {
                        l.Name,
                        l.ParentName,
                        l.Confidence
                    }),
                    DetectedLabels = autoTags,
                    Timestamp = DateTime.UtcNow
                }, new JsonSerializerOptions { WriteIndented = true })
            });

            context.Logger.LogInformation($"SNS notification sent for flagged content: {key}");
        }
        else
        {
            context.Logger.LogInformation($"Content safe: {key}, {autoTags.Count} tags detected");
        }

        await WriteBackToDbAsync(key, isSafe, labelResponse, moderationResponse, context);
    }

    private async Task WriteBackToDbAsync(
        string s3Key,
        bool isSafe,
        DetectLabelsResponse labelResponse,
        DetectModerationLabelsResponse moderationResponse,
        ILambdaContext context)
    {
        if (_injectedDb is null && _dbOptions is null)
        {
            context.Logger.LogWarning("No database configured — skipping write-back");
            return;
        }

        var db = _injectedDb ?? new ApplicationDbContext(_dbOptions!);
        var ownsDb = _injectedDb is null;
        try
        {
            var asset = await db.MediaAssets.FirstOrDefaultAsync(a => a.S3Key == s3Key);
            if (asset is null)
            {
                context.Logger.LogWarning($"No asset found for S3Key: {s3Key} — skipping write-back");
                return;
            }

            MediaAssetModeration.Apply(
                asset,
                isSafe,
                labelResponse.Labels ?? new List<Label>(),
                moderationResponse.ModerationLabels ?? new List<ModerationLabel>(),
                DateTime.UtcNow);

            await db.SaveChangesAsync();

            context.Logger.LogInformation($"Write-back complete for asset {asset.Id} (safe={isSafe})");
        }
        catch (Exception ex)
        {
            context.Logger.LogError($"DB write-back failed for {s3Key}: {ex.Message}");
        }
        finally
        {
            if (ownsDb) await db.DisposeAsync();
        }
    }
}
