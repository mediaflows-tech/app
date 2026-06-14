using Amazon.S3;
using Amazon.S3.Model;
using MediaFlows.Shared.Configuration;
using MediaFlows.Shared.Interfaces;
using Microsoft.Extensions.Options;

namespace MediaFlows.Web.Services;

public class S3StorageService : IS3StorageService
{
    private readonly IAmazonS3 _s3Client;
    private readonly S3Settings _settings;

    public S3StorageService(IAmazonS3 s3Client, IOptions<S3Settings> settings)
    {
        _s3Client = s3Client;
        _settings = settings.Value;
    }

    public string GeneratePresignedPutUrl(string key, string contentType, TimeSpan expiry)
    {
        var request = new GetPreSignedUrlRequest
        {
            BucketName = _settings.BucketName,
            Key = key,
            Verb = HttpVerb.PUT,
            Expires = DateTime.UtcNow.Add(expiry),
            ContentType = contentType
        };

        return _s3Client.GetPreSignedURL(request);
    }

    public async Task<bool> ObjectExistsAsync(string key)
    {
        try
        {
            await _s3Client.GetObjectMetadataAsync(_settings.BucketName, key);
            return true;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    public async Task<(string ContentType, long ContentLength)> GetObjectMetadataAsync(string key)
    {
        var metadata = await _s3Client.GetObjectMetadataAsync(_settings.BucketName, key);
        return (metadata.Headers.ContentType, metadata.Headers.ContentLength);
    }

    public async Task DeleteObjectAsync(string key)
    {
        await _s3Client.DeleteObjectAsync(_settings.BucketName, key);
    }

    public async Task CopyObjectAsync(string sourceKey, string destinationKey)
    {
        await _s3Client.CopyObjectAsync(new CopyObjectRequest
        {
            SourceBucket = _settings.BucketName,
            SourceKey = sourceKey,
            DestinationBucket = _settings.BucketName,
            DestinationKey = destinationKey
        });
    }

    public string GeneratePresignedGetUrl(string key, string downloadFilename, TimeSpan expiry)
    {
        var request = new GetPreSignedUrlRequest
        {
            BucketName = _settings.BucketName,
            Key = key,
            Verb = HttpVerb.GET,
            Expires = DateTime.UtcNow.Add(expiry),
            ResponseHeaderOverrides =
            {
                ContentDisposition = $"attachment; filename=\"{downloadFilename}\""
            }
        };

        return _s3Client.GetPreSignedURL(request);
    }

    public string GetPublicUrl(string key)
    {
        if (HasUsableCloudFrontDomain(_settings.CloudFrontDomain))
            return $"https://{_settings.CloudFrontDomain.Trim()}/{key}";

        return $"https://{_settings.BucketName}.s3.{_settings.Region}.amazonaws.com/{key}";
    }

    private static bool HasUsableCloudFrontDomain(string? domain)
    {
        if (string.IsNullOrWhiteSpace(domain))
            return false;

        var normalized = domain.Trim().ToLowerInvariant();

        return normalized != "d1234567890.cloudfront.net";
    }
}
