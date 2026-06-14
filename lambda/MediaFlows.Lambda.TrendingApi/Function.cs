using System.Text.Json;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.XRay.Recorder.Handlers.AwsSdk;
using MediaFlows.Shared.Models.DynamoDb;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace MediaFlows.Lambda.TrendingApi;

public class Function
{
    private readonly IDynamoDBContext _dynamoContext;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static readonly Dictionary<string, string> ResponseHeaders = new()
    {
        ["Content-Type"] = "application/json",
        ["Access-Control-Allow-Origin"] = "*",
        ["Access-Control-Allow-Methods"] = "GET,OPTIONS",
        ["Access-Control-Allow-Headers"] = "Content-Type,Authorization"
    };

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

    public async Task<APIGatewayProxyResponse> FunctionHandler(APIGatewayProxyRequest request, ILambdaContext context)
    {
        try
        {
            for (var offset = 1; offset <= 7; offset++)
            {
                var bucket = DateTime.UtcNow.AddDays(-offset).ToString("yyyy-MM-dd");
                var items = await _dynamoContext
                    .QueryAsync<TrendingDataItem>(bucket)
                    .GetRemainingAsync();
                if (items.Any())
                {
                    var sorted = items.OrderByDescending(i => i.Score).ToList();
                    return Ok(new { bucket, items = sorted });
                }
            }
            return Ok(new { items = Array.Empty<TrendingDataItem>() });
        }
        catch (Exception ex)
        {
            context.Logger.LogError($"Trending query error: {ex.Message}");
            return new APIGatewayProxyResponse
            {
                StatusCode = 500,
                Headers = ResponseHeaders,
                Body = JsonSerializer.Serialize(new { error = "Internal server error" }, JsonOpts)
            };
        }
    }

    private static APIGatewayProxyResponse Ok(object body) => new()
    {
        StatusCode = 200,
        Headers = ResponseHeaders,
        Body = JsonSerializer.Serialize(body, JsonOpts)
    };
}
