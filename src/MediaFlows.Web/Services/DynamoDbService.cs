using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;
using Amazon.DynamoDBv2.Model;
using MediaFlows.Shared.Configuration;
using MediaFlows.Shared.Interfaces;
using MediaFlows.Shared.Models.DynamoDb;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MediaFlows.Web.Services;

public class DynamoDbService : IDynamoDbService
{
    private readonly IAmazonDynamoDB _client;
    private readonly IDynamoDBContext _context;
    private readonly ILogger<DynamoDbService> _logger;
    private readonly string _tablePrefix;

    public DynamoDbService(
        IAmazonDynamoDB client,
        IDynamoDBContext context,
        ILogger<DynamoDbService> logger,
        IOptions<DynamoDbSettings> settings)
    {
        _client = client;
        _context = context;
        _logger = logger;
        _tablePrefix = settings.Value.TableNamePrefix;
    }

    public async Task<int> IncrementViewCountAsync(string assetId)
    {
        try
        {
            var response = await _client.UpdateItemAsync(new UpdateItemRequest
            {
                TableName = $"{_tablePrefix}ViewCounters",
                Key = new Dictionary<string, AttributeValue>
                {
                    { "AssetId", new AttributeValue { S = assetId } }
                },
                UpdateExpression = "SET ViewCount = if_not_exists(ViewCount, :zero) + :incr, " +
                                   "LastViewedAt = :now",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    { ":incr", new AttributeValue { N = "1" } },
                    { ":zero", new AttributeValue { N = "0" } },
                    { ":now", new AttributeValue { S = DateTime.UtcNow.ToString("o") } }
                },
                ReturnValues = ReturnValue.UPDATED_NEW
            });

            return int.Parse(response.Attributes["ViewCount"].N);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to increment view count for asset {AssetId}", assetId);
            return 0;
        }
    }

    public async Task<int> GetViewCountAsync(string assetId)
    {
        try
        {
            var response = await _client.GetItemAsync(new GetItemRequest
            {
                TableName = $"{_tablePrefix}ViewCounters",
                Key = new Dictionary<string, AttributeValue>
                {
                    { "AssetId", new AttributeValue { S = assetId } }
                },
                ProjectionExpression = "ViewCount"
            });

            if (response.Item == null || !response.Item.ContainsKey("ViewCount"))
                return 0;

            return int.Parse(response.Item["ViewCount"].N);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get view count for asset {AssetId}", assetId);
            return 0;
        }
    }

    public async Task<List<TrendingDataItem>> GetTrendingAsync(string date, int limit = 20)
    {
        try
        {
            // Hash-key-only query; sort + cap in-memory. The bucket holds at
            // most 50 rows (aggregator's Take(50)) so this is cheap and avoids
            // the BeginsWith("") range-key filter which DynamoDB rejects.
            var results = await _context.QueryAsync<TrendingDataItem>(date)
                .GetRemainingAsync();

            return results
                .OrderByDescending(r => r.Score)
                .Take(limit)
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get trending data for date {Date}", date);
            return new List<TrendingDataItem>();
        }
    }

    public async Task RecordActivityAsync(string userId, string action, string entityType, string entityId, string details)
    {
        try
        {
            var item = new ActivityFeedItem
            {
                UserId = userId,
                Timestamp = DateTime.UtcNow.ToString("o"),
                Action = action,
                EntityType = entityType,
                EntityId = entityId,
                Details = details,
                ExpiresAt = new DateTimeOffset(DateTime.UtcNow.AddDays(30)).ToUnixTimeSeconds()
            };

            await _context.SaveAsync(item);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to record activity for user {UserId}", userId);
        }
    }

    public async Task AddActivityFeedItemAsync(ActivityFeedItem item)
    {
        try
        {
            await _context.SaveAsync(item);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to add activity feed item");
        }
    }

    public async Task<List<ActivityFeedItem>> GetActivityFeedAsync(string userId, int limit = 50)
    {
        try
        {
            var results = await _context.QueryAsync<ActivityFeedItem>(
                userId,
                QueryOperator.BeginsWith,
                new[] { "" },
                new DynamoDBOperationConfig
                {
                    BackwardQuery = true
                })
                .GetRemainingAsync();

            return results.Take(limit).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get activity feed for user {UserId}", userId);
            return new List<ActivityFeedItem>();
        }
    }
}
