using Amazon.Lambda.S3Events;
using Amazon.Lambda.SQSEvents;
using Amazon.Lambda.TestUtilities;
using Amazon.Rekognition;
using Amazon.Rekognition.Model;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using FluentAssertions;
using MediaFlows.Data;
using MediaFlows.Lambda.ContentModerator;
using MediaFlows.Shared.Models.Entities;
using MediaFlows.Shared.Models.Enums;
using MediaFlows.Tests.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Moq;
using System.Text.Json;

namespace MediaFlows.Lambda.Tests;

public class ContentModeratorTests
{
    private readonly Mock<IAmazonRekognition> _rekognition;
    private readonly Mock<IAmazonS3> _s3Client;
    private readonly Mock<IAmazonSimpleNotificationService> _sns;
    private readonly Function _sut;
    private readonly TestLambdaContext _context;

    public ContentModeratorTests()
    {
        _rekognition = new Mock<IAmazonRekognition>();
        _s3Client = new Mock<IAmazonS3>();
        _sns = new Mock<IAmazonSimpleNotificationService>();
        _s3Client.Setup(x => x.GetObjectMetadataAsync(It.IsAny<string>(), It.IsAny<string>(), default))
            .ReturnsAsync(new GetObjectMetadataResponse
            {
                Headers = { ContentType = "image/jpeg" }
            });
        _sut = new Function(_rekognition.Object, _s3Client.Object, _sns.Object,
            "arn:aws:sns:ap-southeast-1:123:content-flagged", "media-bucket");
        _context = new TestLambdaContext();
    }

    private Function CreateSutWithDb(ApplicationDbContext db) =>
        new(_rekognition.Object, _s3Client.Object, _sns.Object,
            "arn:aws:sns:ap-southeast-1:123:content-flagged", "media-bucket", db);

    private static ApplicationDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ReplaceService<IModelCacheKeyFactory, TestModelCacheKeyFactory>()
            .Options;
        return new TestDbContext(options);
    }

    private static MediaAsset SeedAsset(int id, string s3Key, AssetStatus status = AssetStatus.Approved) =>
        new()
        {
            Id = id,
            Title = $"Asset {id}",
            CreatorId = "creator-1",
            S3Key = s3Key,
            ContentType = "image/jpeg",
            FileSize = 1024,
            Status = status
        };

    private void SetupFlaggedRekognition()
    {
        _rekognition.Setup(x => x.DetectLabelsAsync(It.IsAny<DetectLabelsRequest>(), default))
            .ReturnsAsync(new DetectLabelsResponse { Labels = new List<Label>() });
        _rekognition.Setup(x => x.DetectModerationLabelsAsync(It.IsAny<DetectModerationLabelsRequest>(), default))
            .ReturnsAsync(new DetectModerationLabelsResponse
            {
                ModerationLabels = new List<ModerationLabel>
                {
                    new() { Name = "Explicit Nudity", ParentName = "Nudity", Confidence = 85.0f }
                }
            });
        _s3Client.Setup(x => x.CopyObjectAsync(It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string>(), default))
            .ReturnsAsync(new CopyObjectResponse());
        _s3Client.Setup(x => x.DeleteObjectAsync(It.IsAny<string>(), It.IsAny<string>(), default))
            .ReturnsAsync(new DeleteObjectResponse());
        _sns.Setup(x => x.PublishAsync(It.IsAny<PublishRequest>(), default))
            .ReturnsAsync(new PublishResponse());
    }

    /// <summary>
    /// Wraps an S3 event in an SQS message using the actual AWS S3 notification
    /// JSON format (camelCase), not the C# serialization format (PascalCase).
    /// This ensures tests catch case-sensitivity bugs in deserialization.
    /// </summary>
    private static SQSEvent WrapInSqsEvent(string bucket, string key) => new()
    {
        Records = new List<SQSEvent.SQSMessage>
        {
            new()
            {
                Body = JsonSerializer.Serialize(new
                {
                    Records = new[]
                    {
                        new
                        {
                            eventVersion = "2.1",
                            eventSource = "aws:s3",
                            awsRegion = "ap-southeast-1",
                            eventName = "ObjectCreated:Put",
                            s3 = new
                            {
                                s3SchemaVersion = "1.0",
                                bucket = new { name = bucket, arn = $"arn:aws:s3:::{bucket}" },
                                @object = new { key, size = 12345L }
                            }
                        }
                    }
                })
            }
        }
    };

    [Fact]
    public async Task FunctionHandler_SafeContent_DetectsLabelsOnly()
    {
        _rekognition.Setup(x => x.DetectLabelsAsync(It.IsAny<DetectLabelsRequest>(), default))
            .ReturnsAsync(new DetectLabelsResponse
            {
                Labels = new List<Label>
                {
                    new() { Name = "Landscape", Confidence = 98.5f },
                    new() { Name = "Nature", Confidence = 95.0f }
                }
            });

        _rekognition.Setup(x => x.DetectModerationLabelsAsync(It.IsAny<DetectModerationLabelsRequest>(), default))
            .ReturnsAsync(new DetectModerationLabelsResponse
            {
                ModerationLabels = new List<ModerationLabel>()
            });

        await _sut.FunctionHandler(WrapInSqsEvent("source", "uploads/u1/id/photo.jpg"), _context);

        _s3Client.Verify(x => x.CopyObjectAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), default), Times.Never);
        _sns.Verify(x => x.PublishAsync(It.IsAny<PublishRequest>(), default), Times.Never);
    }

    [Fact]
    public async Task FunctionHandler_UnsafeContent_QuarantinesAndNotifies()
    {
        _rekognition.Setup(x => x.DetectLabelsAsync(It.IsAny<DetectLabelsRequest>(), default))
            .ReturnsAsync(new DetectLabelsResponse { Labels = new List<Label>() });

        _rekognition.Setup(x => x.DetectModerationLabelsAsync(It.IsAny<DetectModerationLabelsRequest>(), default))
            .ReturnsAsync(new DetectModerationLabelsResponse
            {
                ModerationLabels = new List<ModerationLabel>
                {
                    new() { Name = "Explicit Nudity", ParentName = "Nudity", Confidence = 85.0f }
                }
            });

        _s3Client.Setup(x => x.CopyObjectAsync(It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string>(), default))
            .ReturnsAsync(new CopyObjectResponse());
        _s3Client.Setup(x => x.DeleteObjectAsync(It.IsAny<string>(), It.IsAny<string>(), default))
            .ReturnsAsync(new DeleteObjectResponse());
        _sns.Setup(x => x.PublishAsync(It.IsAny<PublishRequest>(), default))
            .ReturnsAsync(new PublishResponse());

        await _sut.FunctionHandler(WrapInSqsEvent("source", "uploads/u1/id/bad.jpg"), _context);

        _s3Client.Verify(x => x.CopyObjectAsync("source", "uploads/u1/id/bad.jpg",
            "media-bucket", "quarantine/bad.jpg", default), Times.Once);
        _s3Client.Verify(x => x.DeleteObjectAsync("source", "uploads/u1/id/bad.jpg", default), Times.Once);

        _sns.Verify(x => x.PublishAsync(It.Is<PublishRequest>(r =>
            r.TopicArn == "arn:aws:sns:ap-southeast-1:123:content-flagged" &&
            r.Subject == "Content Flagged - MediaFlows"), default), Times.Once);
    }

    [Fact]
    public async Task FunctionHandler_UrlDecodesS3Key()
    {
        _s3Client.Setup(x => x.GetObjectMetadataAsync("bucket", "uploads/u1/id/my photo.jpg", default))
            .ReturnsAsync(new GetObjectMetadataResponse
            {
                Headers = { ContentType = "image/jpeg" }
            });
        _rekognition.Setup(x => x.DetectLabelsAsync(It.IsAny<DetectLabelsRequest>(), default))
            .ReturnsAsync(new DetectLabelsResponse { Labels = new List<Label>() });
        _rekognition.Setup(x => x.DetectModerationLabelsAsync(It.IsAny<DetectModerationLabelsRequest>(), default))
            .ReturnsAsync(new DetectModerationLabelsResponse { ModerationLabels = new List<ModerationLabel>() });

        await _sut.FunctionHandler(WrapInSqsEvent("bucket", "uploads/u1/id/my+photo.jpg"), _context);

        _rekognition.Verify(x => x.DetectLabelsAsync(
            It.Is<DetectLabelsRequest>(r => r.Image.S3Object.Name == "uploads/u1/id/my photo.jpg"), default), Times.Once);
    }

    [Fact]
    public async Task FunctionHandler_UnsupportedContentType_SkipsRekognition()
    {
        _s3Client.Setup(x => x.GetObjectMetadataAsync("bucket", "uploads/u1/id/animated.gif", default))
            .ReturnsAsync(new GetObjectMetadataResponse
            {
                Headers = { ContentType = "image/gif" }
            });

        await _sut.FunctionHandler(WrapInSqsEvent("bucket", "uploads/u1/id/animated.gif"), _context);

        _rekognition.Verify(x => x.DetectLabelsAsync(It.IsAny<DetectLabelsRequest>(), default), Times.Never);
        _rekognition.Verify(x => x.DetectModerationLabelsAsync(It.IsAny<DetectModerationLabelsRequest>(), default), Times.Never);
        _sns.Verify(x => x.PublishAsync(It.IsAny<PublishRequest>(), default), Times.Never);
    }

    [Fact]
    public async Task FunctionHandler_UnsafeContent_QuarantinesAssetInDb()
    {
        using var db = CreateInMemoryContext();
        db.MediaAssets.Add(SeedAsset(1, "uploads/u1/id/bad.jpg"));
        await db.SaveChangesAsync();
        SetupFlaggedRekognition();

        var sut = CreateSutWithDb(db);

        await sut.FunctionHandler(WrapInSqsEvent("source", "uploads/u1/id/bad.jpg"), _context);

        (await db.MediaAssets.SingleAsync(a => a.Id == 1)).Status.Should().Be(AssetStatus.Quarantined);
    }

    [Fact]
    public async Task FunctionHandler_AssetNotFoundInDb_DoesNotThrowAndDoesNotMutate()
    {
        using var db = CreateInMemoryContext();
        db.MediaAssets.Add(SeedAsset(42, "uploads/u1/id/elsewhere.jpg"));
        await db.SaveChangesAsync();
        SetupFlaggedRekognition();

        var sut = CreateSutWithDb(db);

        var act = async () => await sut.FunctionHandler(
            WrapInSqsEvent("source", "uploads/u1/id/nope.jpg"), _context);

        await act.Should().NotThrowAsync();
        (await db.MediaAssets.SingleAsync(a => a.Id == 42)).Status.Should().Be(AssetStatus.Approved);
    }

    [Fact]
    public async Task FunctionHandler_OnlyAssetMatchingS3KeyIsMutated()
    {
        using var db = CreateInMemoryContext();
        db.MediaAssets.AddRange(
            SeedAsset(1, "uploads/u1/id/target.jpg"),
            SeedAsset(2, "uploads/u1/id/bystander.jpg"));
        await db.SaveChangesAsync();
        SetupFlaggedRekognition();

        var sut = CreateSutWithDb(db);

        await sut.FunctionHandler(WrapInSqsEvent("source", "uploads/u1/id/target.jpg"), _context);

        (await db.MediaAssets.SingleAsync(a => a.Id == 1)).Status.Should().Be(AssetStatus.Quarantined);
        (await db.MediaAssets.SingleAsync(a => a.Id == 2)).Status.Should().Be(AssetStatus.Approved);
    }

    [Fact]
    public async Task FunctionHandler_NoDbContextConfigured_CompletesWithoutThrowing()
    {
        SetupFlaggedRekognition();

        var act = async () => await _sut.FunctionHandler(
            WrapInSqsEvent("source", "uploads/u1/id/bad.jpg"), _context);

        await act.Should().NotThrowAsync();
    }
}
