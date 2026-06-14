using Amazon.DynamoDBv2.DataModel;

namespace MediaFlows.Shared.Models.DynamoDb;

[DynamoDBTable("ViewCounters")]
public class ViewCounter
{
    [DynamoDBHashKey]
    public string AssetId { get; set; } = null!;

    [DynamoDBProperty]
    public int ViewCount { get; set; }

    [DynamoDBProperty]
    public DateTime LastViewedAt { get; set; }
}
