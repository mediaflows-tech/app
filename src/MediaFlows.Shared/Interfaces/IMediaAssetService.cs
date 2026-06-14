using MediaFlows.Shared.DTOs;
using MediaFlows.Shared.Models.Entities;
using MediaFlows.Shared.Models.Enums;
using MediaFlows.Shared.Models.ValueObjects;

namespace MediaFlows.Shared.Interfaces;

public interface IMediaAssetService
{
    Task<PagedResult<MediaAssetSummaryDto>> GetPagedAssetsAsync(
        string? creatorId, AssetStatus? status, int page, int pageSize,
        string? fileType = null, string? sort = null);
    Task<List<MediaAssetSummaryDto>> GetByIdsAsync(
        IReadOnlyList<int> assetIds, string? fileType = null);
    Task<MediaAsset?> GetByIdAsync(int id);
    Task<MediaAsset> CreateAsync(MediaAsset asset);
    Task UpdateMetadataAsync(int assetId, MediaMetadata metadata);
    Task UpdateStatusAsync(int assetId, AssetStatus newStatus, string updatedBy);
    Task AddTagAsync(int assetId, string tagName, float confidence = 1.0f);
    Task RemoveTagAsync(int assetId, string tagName);
    Task UpdateTitleAsync(int assetId, string newTitle);
    Task UpdateDescriptionAsync(int assetId, string? newDescription);
    Task DeleteAsync(int assetId, string deletedBy);
}
