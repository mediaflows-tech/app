using MediaFlows.Shared.DTOs;

namespace MediaFlows.Shared.Interfaces;

public interface IBookmarkService
{
    Task<bool> ToggleBookmarkAsync(string userId, int assetId);
    Task<bool> IsBookmarkedAsync(string userId, int assetId);
    Task<PagedResult<MediaAssetSummaryDto>> GetUserBookmarksAsync(string userId, int page, int pageSize);
    Task<int> GetBookmarkCountAsync(string userId);
}
