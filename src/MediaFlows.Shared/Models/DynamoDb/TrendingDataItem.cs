using Amazon.DynamoDBv2.DataModel;

namespace MediaFlows.Shared.Models.DynamoDb;

[DynamoDBTable("TrendingData")]
public class TrendingDataItem
{
    [DynamoDBHashKey]
    public string TimeBucket { get; set; } = null!;

    [DynamoDBRangeKey]
    public string ScoreAssetId { get; set; } = null!;

    [DynamoDBProperty]
    public int AssetId { get; set; }

    [DynamoDBProperty]
    public int Score { get; set; }

    [DynamoDBProperty]
    public string Title { get; set; } = null!;

    [DynamoDBProperty]
    public string ThumbnailUrl { get; set; } = null!;

    [DynamoDBProperty]
    public long ExpiresAt { get; set; }
}
