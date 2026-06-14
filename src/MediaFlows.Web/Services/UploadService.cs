using Amazon.S3;
using Amazon.S3.Model;
using MediaFlows.Shared.Configuration;
using MediaFlows.Shared.DTOs;
using MediaFlows.Shared.Interfaces;
using MediaFlows.Shared.Models.Entities;
using MediaFlows.Shared.Models.Enums;
using MediaFlows.Shared.Models.ValueObjects;
using Microsoft.Extensions.Options;

namespace MediaFlows.Web.Services;

public class UploadService : IUploadService
{
    private readonly IAmazonS3 _s3Client;
    private readonly S3Settings _s3Config;
    private readonly IRekognitionService _rekognitionService;
    private readonly RekognitionSettings _rekognitionSettings;
    private readonly ILogger<UploadService> _logger;

    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/webp", "image/gif", "image/svg+xml", "image/bmp", "image/tiff",
        "video/mp4", "video/quicktime", "video/webm", "video/x-msvideo", "video/x-matroska",
        "audio/mpeg", "audio/wav", "audio/flac", "audio/x-flac", "audio/aac", "audio/ogg",
        "application/pdf"
    };

    private static readonly HashSet<string> RekognitionImageContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png"
    };

    public UploadService(
        IAmazonS3 s3Client,
        IOptions<S3Settings> s3Config,
        IRekognitionService rekognitionService,
        IOptions<RekognitionSettings> rekognitionSettings,
        ILogger<UploadService> logger)
    {
        _s3Client = s3Client;
        _s3Config = s3Config.Value;
        _rekognitionService = rekognitionService;
        _rekognitionSettings = rekognitionSettings.Value;
        _logger = logger;
    }

    public UploadPresignedUrlResponse GeneratePresignedUrl(
        string userId, string fileName, string contentType)
    {
        if (!AllowedContentTypes.Contains(contentType))
            throw new ArgumentException($"Content type '{contentType}' is not allowed.");

        var sanitizedName = SanitizeFileName(fileName);
        var key = $"uploads/{userId}/{Guid.NewGuid()}/{sanitizedName}";

        var request = new GetPreSignedUrlRequest
        {
            BucketName = _s3Config.BucketName,
            Key = key,
            Verb = HttpVerb.PUT,
            Expires = DateTime.UtcNow.AddMinutes(15),
            ContentType = contentType
        };

        var url = _s3Client.GetPreSignedURL(request);

        _logger.LogInformation("Generated presigned URL for user {UserId}, file {FileName}", userId, sanitizedName);

        return new UploadPresignedUrlResponse
        {
            UploadUrl = url,
            S3Key = key,
            ExpiresAt = DateTime.UtcNow.AddMinutes(15)
        };
    }

    public async Task<MediaAsset> ConfirmUploadAsync(
        UploadConfirmRequest request, string userId)
    {
        GetObjectMetadataResponse metadata;
        try
        {
            metadata = await _s3Client.GetObjectMetadataAsync(_s3Config.BucketName, request.S3Key);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            throw new InvalidOperationException($"S3 object not found: {request.S3Key}");
        }

        var metadataValue = new MediaMetadata
        {
            Format = Path.GetExtension(request.FileName).TrimStart('.')
        };

        if (_rekognitionSettings.Enabled &&
            RekognitionImageContentTypes.Contains(metadata.Headers.ContentType))
        {
            try
            {
                var autoTags = await _rekognitionService.DetectLabelsAsync(request.S3Key, 75f);
                var moderation = await _rekognitionService.DetectModerationLabelsAsync(request.S3Key);

                metadataValue.AutoTags = autoTags;
                metadataValue.Moderation = moderation;

                if (!moderation.IsSafe)
                {
                    await DeleteRejectedUploadAsync(request.S3Key);
                    throw new InvalidOperationException(
                        "Upload failed content moderation and was rejected.");
                }
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Content moderation failed for upload {S3Key}", request.S3Key);
                await DeleteRejectedUploadAsync(request.S3Key);
                throw new InvalidOperationException(
                    "Content moderation could not complete. Please try again.");
            }
        }

        var asset = new MediaAsset
        {
            CreatorId = userId,
            Title = Path.GetFileNameWithoutExtension(request.FileName),
            S3Key = request.S3Key,
            ContentType = metadata.Headers.ContentType,
            FileSize = metadata.Headers.ContentLength,
            Status = AssetStatus.Draft,
            Metadata = metadataValue
        };

        _logger.LogInformation("Confirmed upload for user {UserId}, key {S3Key}, size {Size}",
            userId, request.S3Key, metadata.Headers.ContentLength);

        return asset;
    }

    private async Task DeleteRejectedUploadAsync(string s3Key)
    {
        try
        {
            await _s3Client.DeleteObjectAsync(_s3Config.BucketName, s3Key);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete rejected upload {S3Key}", s3Key);
        }
    }

    public static string SanitizeFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return "unnamed";

        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(fileName
            .Where(c => !invalidChars.Contains(c))
            .ToArray());

        if (string.IsNullOrWhiteSpace(sanitized))
            return "unnamed";

        if (sanitized.Length > 200)
        {
            var ext = Path.GetExtension(sanitized);
            var nameWithoutExt = Path.GetFileNameWithoutExtension(sanitized);
            var maxNameLength = 200 - ext.Length;
            sanitized = nameWithoutExt[..Math.Min(nameWithoutExt.Length, maxNameLength)] + ext;
        }

        return sanitized;
    }
}
