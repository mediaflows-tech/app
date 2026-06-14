using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.XRay.Recorder.Handlers.AwsSdk;
using MediaFlows.Data;
using MediaFlows.Shared.Models.Enums;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace MediaFlows.Lambda.SearchApi;

public class Function
{
    private readonly ApplicationDbContext _db;

    static Function()
    {
        AWSSDKHandler.RegisterXRayForAllServices();
    }

    public Function()
    {
        var connectionString = Environment.GetEnvironmentVariable("DB_CONNECTION_STRING")
            ?? throw new InvalidOperationException("DB_CONNECTION_STRING environment variable not set");
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        _db = new ApplicationDbContext(options);
    }

    public Function(ApplicationDbContext db) => _db = db;

    public async Task<APIGatewayProxyResponse> FunctionHandler(
        APIGatewayProxyRequest request, ILambdaContext context)
    {
        try
        {
            var queryParams = request.QueryStringParameters ?? new Dictionary<string, string>();
            var query = queryParams.TryGetValue("q", out var q) ? q ?? "" : "";
            var page = queryParams.TryGetValue("page", out var pg) && int.TryParse(pg, out var p) ? p : 1;
            queryParams.TryGetValue("type", out var fileType);
            var pageSize = 20;

            context.Logger.LogInformation($"Search query: '{query}', page: {page}, type: {fileType}");

            var baseQuery = _db.MediaAssets
                .Where(a => a.Status == AssetStatus.Published);

            if (!string.IsNullOrWhiteSpace(query))
            {
                baseQuery = baseQuery
                    .Where(a => a.SearchVector.Matches(
                        EF.Functions.WebSearchToTsQuery("english", query)));
            }

            if (!string.IsNullOrEmpty(fileType))
                baseQuery = baseQuery.Where(a => a.ContentType.StartsWith(fileType));

            var totalCount = await baseQuery.CountAsync();

            var results = await baseQuery
                .OrderByDescending(a => string.IsNullOrWhiteSpace(query) ? 0 :
                    a.SearchVector.Rank(EF.Functions.WebSearchToTsQuery("english", query)))
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(a => new
                {
                    a.Id,
                    a.Title,
                    a.Description,
                    a.ThumbnailUrl,
                    a.ContentType,
                    a.CreatedAt,
                    CreatorName = a.Creator.DisplayName,
                    Headline = string.IsNullOrWhiteSpace(query)
                        ? a.Description
                        : EF.Functions.ToTsQuery("english", query)
                            .GetResultHeadline(a.Description ?? "")
                })
                .AsNoTracking()
                .ToListAsync();

            var responseBody = new
            {
                items = results,
                totalCount,
                page,
                pageSize,
                hasMore = (page * pageSize) < totalCount,
                totalPages = (int)Math.Ceiling((double)totalCount / pageSize)
            };

            return new APIGatewayProxyResponse
            {
                StatusCode = 200,
                Headers = new Dictionary<string, string>
                {
                    { "Content-Type", "application/json" },
                    { "Access-Control-Allow-Origin", "*" },
                    { "Access-Control-Allow-Methods", "GET,OPTIONS" },
                    { "Access-Control-Allow-Headers", "Content-Type,Authorization" }
                },
                Body = JsonSerializer.Serialize(responseBody, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                })
            };
        }
        catch (Exception ex)
        {
            context.Logger.LogError($"Search error: {ex.Message}");

            return new APIGatewayProxyResponse
            {
                StatusCode = 500,
                Headers = new Dictionary<string, string>
                {
                    { "Content-Type", "application/json" },
                    { "Access-Control-Allow-Origin", "*" }
                },
                Body = JsonSerializer.Serialize(new { error = "Internal server error" })
            };
        }
    }
}
