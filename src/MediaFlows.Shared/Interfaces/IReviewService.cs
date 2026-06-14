using MediaFlows.Shared.DTOs;
using MediaFlows.Shared.Models.Entities;
using MediaFlows.Shared.Models.Enums;

namespace MediaFlows.Shared.Interfaces;

public interface IReviewService
{
    Task<PagedResult<ReviewListItemDto>> GetPendingReviewsAsync(
        AssetStatus? status, string? creatorId, string? contentType,
        string? sortBy, string? sortDir, int page, int pageSize);
    Task<ReviewDetailsDto?> GetReviewDetailsAsync(int assetId);
    Task SubmitDecisionAsync(int assetId, ReviewDecision decision, string reviewerId, string? comments);
    Task BatchDecisionAsync(int[] assetIds, ReviewDecision decision, string reviewerId, string? comments);
    Task ApproveAndPublishAsync(int assetId, string reviewerId, string? comments);
    Task ApproveAndScheduleAsync(int assetId, DateTime scheduledAt, string reviewerId, string? comments);
    Task<int> BatchApproveAndPublishAsync(int[] assetIds, string reviewerId, string? comments);
    Task<int> GetPendingCountAsync();
    Task<int> GetApprovedCountAsync();
    Task<int> GetRejectedCountAsync();
    Task<List<ScheduledPublishDto>> GetScheduledAssetsAsync(DateTime start, DateTime end);
    Task SchedulePublishAsync(int assetId, DateTime scheduledAt, string editorId);
    Task ReschedulePublishAsync(int assetId, DateTime newScheduledAt, string editorId);
    Task UnscheduleAsync(int assetId, string editorId);
    Task BatchScheduleAsync(int[] assetIds, DateTime scheduledAt, string editorId);
    Task BatchApproveAndScheduleAsync(int[] assetIds, DateTime scheduledAt, string reviewerId);
    Task PublishNowAsync(int assetId, string editorId);
    Task<int> PublishDueScheduledAsync();
    Task RejectApprovedAsync(int assetId, string reviewerId, string comments);
    Task<List<ScheduledPublishDto>> GetAvailableAssetsAsync();
}
