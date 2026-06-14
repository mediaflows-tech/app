using MediaFlows.Shared.DTOs;
using MediaFlows.Shared.Models.Entities;

namespace MediaFlows.Shared.Interfaces;

public interface IUploadService
{
    UploadPresignedUrlResponse GeneratePresignedUrl(string userId, string fileName, string contentType);
    Task<MediaAsset> ConfirmUploadAsync(UploadConfirmRequest request, string userId);
}
