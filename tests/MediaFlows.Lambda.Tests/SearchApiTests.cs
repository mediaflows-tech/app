using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.TestUtilities;
using FluentAssertions;
using MediaFlows.Data;
using MediaFlows.Lambda.SearchApi;
using MediaFlows.Shared.Models.Entities;
using MediaFlows.Shared.Models.Enums;
using MediaFlows.Tests.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using System.Text.Json;
using Xunit;

namespace MediaFlows.Lambda.Tests;

public class SearchApiTests
{
    private ApplicationDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ReplaceService<IModelCacheKeyFactory, TestModelCacheKeyFactory>()
            .Options;
        var db = new TestDbContext(options);

        db.AppUsers.Add(new AppUser
        {
            CognitoSub = "creator-1",
            Email = "c@test.com",
            DisplayName = "Creator",
            Role = "ContentCreator"
        });
        db.MediaAssets.AddRange(
            new MediaAsset
            {
                Id = 1,
                Title = "Sunset Photo",
                Description = "Beautiful sunset over the ocean",
                CreatorId = "creator-1",
                Status = AssetStatus.Published,
                S3Key = "uploads/sunset.jpg",
                ContentType = "image/jpeg",
                FileSize = 2048
            },
            new MediaAsset
            {
                Id = 2,
                Title = "City Skyline",
                Description = "Urban cityscape at night",
                CreatorId = "creator-1",
                Status = AssetStatus.Published,
                S3Key = "uploads/city.jpg",
                ContentType = "image/jpeg",
                FileSize = 3072
            },
            new MediaAsset
            {
                Id = 3,
                Title = "Draft Asset",
                Description = "Not published",
                CreatorId = "creator-1",
                Status = AssetStatus.Draft,
                S3Key = "uploads/draft.jpg",
                ContentType = "image/jpeg",
                FileSize = 1024
            }
        );
        db.SaveChanges();
        return db;
    }

    [Fact]
    public async Task FunctionHandler_EmptyQuery_ReturnsAllApprovedAssets()
    {
        using var db = CreateInMemoryContext();
        var function = new Function(db);
        var context = new TestLambdaContext();

        var request = new APIGatewayProxyRequest
        {
            QueryStringParameters = new Dictionary<string, string> { { "q", "" } }
        };

        var response = await function.FunctionHandler(request, context);

        response.StatusCode.Should().Be(200);
        response.Headers["Content-Type"].Should().Be("application/json");
        response.Headers["Access-Control-Allow-Origin"].Should().Be("*");

        var body = JsonSerializer.Deserialize<JsonElement>(response.Body);
        body.GetProperty("totalCount").GetInt32().Should().Be(2);
    }

    [Fact]
    public async Task FunctionHandler_NullQueryParams_ReturnsResults()
    {
        using var db = CreateInMemoryContext();
        var function = new Function(db);
        var context = new TestLambdaContext();

        var request = new APIGatewayProxyRequest
        {
            QueryStringParameters = null
        };

        var response = await function.FunctionHandler(request, context);

        response.StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task FunctionHandler_PaginationWorks()
    {
        using var db = CreateInMemoryContext();
        var function = new Function(db);
        var context = new TestLambdaContext();

        var request = new APIGatewayProxyRequest
        {
            QueryStringParameters = new Dictionary<string, string>
            {
                { "q", "" },
                { "page", "1" }
            }
        };

        var response = await function.FunctionHandler(request, context);
        response.StatusCode.Should().Be(200);

        var body = JsonSerializer.Deserialize<JsonElement>(response.Body);
        body.GetProperty("page").GetInt32().Should().Be(1);
        body.GetProperty("pageSize").GetInt32().Should().Be(20);
    }

    [Fact]
    public async Task FunctionHandler_TypeFilter_FiltersResults()
    {
        using var db = CreateInMemoryContext();
        db.MediaAssets.Add(new MediaAsset
        {
            Id = 10,
            Title = "Video",
            Description = "test video",
            CreatorId = "creator-1",
            Status = AssetStatus.Published,
            S3Key = "uploads/v.mp4",
            ContentType = "video/mp4",
            FileSize = 5000
        });
        await db.SaveChangesAsync();

        var function = new Function(db);
        var context = new TestLambdaContext();

        var request = new APIGatewayProxyRequest
        {
            QueryStringParameters = new Dictionary<string, string>
            {
                { "q", "" },
                { "type", "video" }
            }
        };

        var response = await function.FunctionHandler(request, context);
        response.StatusCode.Should().Be(200);

        var body = JsonSerializer.Deserialize<JsonElement>(response.Body);
        body.GetProperty("totalCount").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task FunctionHandler_ReturnsCorsHeaders()
    {
        using var db = CreateInMemoryContext();
        var function = new Function(db);
        var context = new TestLambdaContext();

        var request = new APIGatewayProxyRequest
        {
            QueryStringParameters = new Dictionary<string, string> { { "q", "" } }
        };

        var response = await function.FunctionHandler(request, context);

        response.Headers.Should().ContainKey("Access-Control-Allow-Origin");
        response.Headers["Access-Control-Allow-Origin"].Should().Be("*");
        response.Headers.Should().ContainKey("Access-Control-Allow-Methods");
    }
}
