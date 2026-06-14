using Amazon.Lambda.Core;
using Amazon.Lambda.SQSEvents;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.XRay.Recorder.Handlers.AwsSdk;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Formats.Webp;
using System.Text.Json;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace MediaFlows.Lambda.ThumbnailGenerator;

public class Function
{
    private readonly IAmazonS3 _s3Client;
    private readonly string _outputBucket;
    private static readonly int[] ThumbnailSizes = { 150, 300, 600 };

    static Function()
    {
        AWSSDKHandler.RegisterXRayForAllServices();
        Configuration.Default.MemoryAllocator = new SimpleGcMemoryAllocator();
    }

    public Function()
    {
        _s3Client = new AmazonS3Client();
        _outputBucket = Environment.GetEnvironmentVariable("OUTPUT_BUCKET")
            ?? throw new InvalidOperationException("OUTPUT_BUCKET environment variable not set");
    }

    public Function(IAmazonS3 s3Client, string outputBucket)
    {
        _s3Client = s3Client;
        _outputBucket = outputBucket;
    }

    public async Task<SQSBatchResponse> FunctionHandler(SQSEvent sqsEvent, ILambdaContext context)
    {
        var batchItemFailures = new List<SQSBatchResponse.BatchItemFailure>();

        foreach (var record in sqsEvent.Records)
        {
            try
            {
                await ProcessRecordAsync(record, context);
            }
            catch (Exception ex)
            {
                context.Logger.LogError($"Failed to process {record.MessageId}: {ex.Message}");
                batchItemFailures.Add(new SQSBatchResponse.BatchItemFailure
                {
                    ItemIdentifier = record.MessageId
                });
            }
        }

        return new SQSBatchResponse(batchItemFailures);
    }

    private async Task ProcessRecordAsync(SQSEvent.SQSMessage record, ILambdaContext context)
    {
        var s3Event = JsonSerializer.Deserialize<S3EventNotification>(record.Body,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (s3Event?.Records == null || !s3Event.Records.Any())
        {
            context.Logger.LogWarning($"No S3 records in SQS message {record.MessageId}");
            return;
        }

        foreach (var s3Record in s3Event.Records)
        {
            var key = Uri.UnescapeDataString(s3Record.S3.Object.Key.Replace("+", " "));
            var bucket = s3Record.S3.Bucket.Name;

            context.Logger.LogInformation($"Processing: {bucket}/{key}");

            using var response = await _s3Client.GetObjectAsync(bucket, key);
            using var image = await Image.LoadAsync(response.ResponseStream);

            foreach (var size in ThumbnailSizes)
            {
                using var clone = image.Clone(ctx => ctx.Resize(new ResizeOptions
                {
                    Mode = ResizeMode.Max,
                    Size = new Size(size, 0)
                }));

                using var outputStream = new MemoryStream();
                await clone.SaveAsWebpAsync(outputStream, new WebpEncoder { Quality = 80 });
                outputStream.Position = 0;

                var assetId = ExtractAssetIdFromKey(key);
                var thumbnailKey = $"thumbnails/{assetId}/{size}x{size}.webp";

                await _s3Client.PutObjectAsync(new PutObjectRequest
                {
                    BucketName = _outputBucket,
                    Key = thumbnailKey,
                    InputStream = outputStream,
                    ContentType = "image/webp"
                });

                context.Logger.LogInformation($"Created thumbnail: {thumbnailKey}");
            }
        }
    }

    internal static string ExtractAssetIdFromKey(string key)
    {
        var parts = key.Split('/');
        return parts.Length >= 3 ? parts[2] : Path.GetFileNameWithoutExtension(key);
    }
}

public class S3EventNotification
{
    public List<S3EventRecord> Records { get; set; } = new();
}

public class S3EventRecord
{
    public S3Entity S3 { get; set; } = new();
}

public class S3Entity
{
    public S3BucketEntity Bucket { get; set; } = new();
    public S3ObjectEntity Object { get; set; } = new();
}

public class S3BucketEntity
{
    public string Name { get; set; } = "";
}

public class S3ObjectEntity
{
    public string Key { get; set; } = "";
    public long Size { get; set; }
}
