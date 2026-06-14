using MediaFlows.Shared.Models.DynamoDb;

namespace MediaFlows.Shared.Interfaces;

public interface IDynamoDbService
{
    Task<int> IncrementViewCountAsync(string assetId);
    Task<int> GetViewCountAsync(string assetId);
    Task<List<TrendingDataItem>> GetTrendingAsync(string date, int limit = 20);
    Task RecordActivityAsync(string userId, string action, string entityType, string entityId, string details);
    Task AddActivityFeedItemAsync(ActivityFeedItem item);
    Task<List<ActivityFeedItem>> GetActivityFeedAsync(string userId, int limit = 50);
}
