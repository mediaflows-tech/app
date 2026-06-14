namespace MediaFlows.Shared.Interfaces;

public interface IS3StorageService
{
    string GeneratePresignedPutUrl(string key, string contentType, TimeSpan expiry);
    Task<bool> ObjectExistsAsync(string key);
    Task<(string ContentType, long ContentLength)> GetObjectMetadataAsync(string key);
    Task DeleteObjectAsync(string key);
    Task CopyObjectAsync(string sourceKey, string destinationKey);
    string GetPublicUrl(string key);
    string GeneratePresignedGetUrl(string key, string downloadFilename, TimeSpan expiry);
}
