using Amazon.DynamoDBv2.DataModel;

namespace MediaFlows.Shared.Models.DynamoDb;

[DynamoDBTable("ActivityFeed")]
public class ActivityFeedItem
{
    [DynamoDBHashKey]
    public string UserId { get; set; } = null!;

    [DynamoDBRangeKey]
    public string Timestamp { get; set; } = null!;

    [DynamoDBProperty]
    public string Action { get; set; } = null!;

    [DynamoDBProperty]
    public string EntityType { get; set; } = null!;

    [DynamoDBProperty]
    public string EntityId { get; set; } = null!;

    [DynamoDBProperty]
    public string Details { get; set; } = null!;

    [DynamoDBProperty]
    public long ExpiresAt { get; set; }
}
