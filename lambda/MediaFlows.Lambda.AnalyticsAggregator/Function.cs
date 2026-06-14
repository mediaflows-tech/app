using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.Lambda.Core;
using Amazon.XRay.Recorder.Handlers.AwsSdk;
using MediaFlows.Shared.Models.DynamoDb;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace MediaFlows.Lambda.AnalyticsAggregator;

public class Function
{
    private readonly IDynamoDBContext _dynamoContext;

    static Function()
    {
        AWSSDKHandler.RegisterXRayForAllServices();
    }

    public Function()
    {
        var client = new AmazonDynamoDBClient();
        var prefix = Environment.GetEnvironmentVariable("DYNAMODB_TABLE_PREFIX") ?? "";
        _dynamoContext = new DynamoDBContextBuilder()
            .WithDynamoDBClient(() => client)
            .ConfigureContext(c => c.TableNamePrefix = prefix)
            .Build();
    }

    public Function(IDynamoDBContext dynamoContext)
    {
        _dynamoContext = dynamoContext;
    }

    public async Task FunctionHandler(object input, ILambdaContext context)
    {
        var yesterday = DateTime.UtcNow.AddDays(-1).ToString("yyyy-MM-dd");
        context.Logger.LogInformation($"Aggregating analytics for: {yesterday}");

        var scanConditions = new List<ScanCondition>();
        var counters = await _dynamoContext
            .ScanAsync<ViewCounter>(scanConditions)
            .GetRemainingAsync();

        context.Logger.LogInformation($"Found {counters.Count} view counters");

        if (counters.Count == 0)
        {
            context.Logger.LogInformation("No view counters to aggregate. Exiting.");
            return;
        }

        var trending = counters
            .OrderByDescending(c => c.ViewCount)
            .Take(50)
            .Select(c => new TrendingDataItem
            {
                TimeBucket = yesterday,
                ScoreAssetId = $"{c.ViewCount:D6}#{c.AssetId}",
                AssetId = int.TryParse(c.AssetId, out var id) ? id : 0,
                Score = c.ViewCount,
                Title = $"Asset {c.AssetId}",
                ThumbnailUrl = "",
                ExpiresAt = new DateTimeOffset(DateTime.UtcNow.AddDays(7)).ToUnixTimeSeconds()
            })
            .ToList();

        var batchWrite = _dynamoContext.CreateBatchWrite<TrendingDataItem>();
        foreach (var item in trending)
        {
            batchWrite.AddPutItem(item);
        }
        await _dynamoContext.ExecuteBatchWriteAsync(new IBatchWrite[] { batchWrite });

        context.Logger.LogInformation($"Wrote {trending.Count} trending items for {yesterday}");

        var oldDate = DateTime.UtcNow.AddDays(-8).ToString("yyyy-MM-dd");
        try
        {
            var oldItems = await _dynamoContext
                .QueryAsync<TrendingDataItem>(oldDate)
                .GetRemainingAsync();
            if (oldItems.Any())
            {
                var deleteBatch = _dynamoContext.CreateBatchWrite<TrendingDataItem>();
                foreach (var item in oldItems)
                    deleteBatch.AddDeleteItem(item);
                await _dynamoContext.ExecuteBatchWriteAsync(new IBatchWrite[] { deleteBatch });
                context.Logger.LogInformation($"Cleaned up {oldItems.Count} old trending items from {oldDate}");
            }
        }
        catch (Exception ex)
        {
            context.Logger.LogWarning($"Failed to clean old trending data: {ex.Message}");
        }
    }
}
