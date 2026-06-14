using Amazon.Lambda.SQSEvents;
using Amazon.Lambda.TestUtilities;
using Amazon.S3;
using Amazon.S3.Model;
using FluentAssertions;
using MediaFlows.Lambda.ThumbnailGenerator;
using Moq;
using SixLabors.ImageSharp.Formats.Png;
using System.Text.Json;

namespace MediaFlows.Lambda.Tests;

public class ThumbnailGeneratorTests
{
    private readonly Mock<IAmazonS3> _s3Client;
    private readonly Function _sut;
    private readonly TestLambdaContext _context;

    public ThumbnailGeneratorTests()
    {
        _s3Client = new Mock<IAmazonS3>();
        _sut = new Function(_s3Client.Object, "output-bucket");
        _context = new TestLambdaContext();
    }

    [Fact]
    public async Task FunctionHandler_GeneratesThreeThumbnails()
    {
        using var image = new SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32>(100, 100);
        using var imageStream = new MemoryStream();
        await image.SaveAsync(imageStream, new PngEncoder());
        imageStream.Position = 0;

        var s3Event = new S3EventNotification
        {
            Records = new List<S3EventRecord>
            {
                new()
                {
                    S3 = new S3Entity
                    {
                        Bucket = new S3BucketEntity { Name = "source-bucket" },
                        Object = new S3ObjectEntity { Key = "uploads/user1/abc-uuid/photo.jpg" }
                    }
                }
            }
        };

        var sqsEvent = new SQSEvent
        {
            Records = new List<SQSEvent.SQSMessage>
            {
                new()
                {
                    MessageId = "msg-1",
                    Body = JsonSerializer.Serialize(s3Event)
                }
            }
        };

        _s3Client.Setup(x => x.GetObjectAsync("source-bucket", "uploads/user1/abc-uuid/photo.jpg", default))
            .ReturnsAsync(new GetObjectResponse
            {
                ResponseStream = imageStream
            });

        _s3Client.Setup(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), default))
            .ReturnsAsync(new PutObjectResponse());

        var result = await _sut.FunctionHandler(sqsEvent, _context);

        result.BatchItemFailures.Should().BeEmpty();
        _s3Client.Verify(x => x.PutObjectAsync(
            It.Is<PutObjectRequest>(r =>
                r.BucketName == "output-bucket" &&
                r.ContentType == "image/webp"),
            default), Times.Exactly(3));

        _s3Client.Verify(x => x.PutObjectAsync(
            It.Is<PutObjectRequest>(r => r.Key == "thumbnails/abc-uuid/150x150.webp"), default), Times.Once);
        _s3Client.Verify(x => x.PutObjectAsync(
            It.Is<PutObjectRequest>(r => r.Key == "thumbnails/abc-uuid/300x300.webp"), default), Times.Once);
        _s3Client.Verify(x => x.PutObjectAsync(
            It.Is<PutObjectRequest>(r => r.Key == "thumbnails/abc-uuid/600x600.webp"), default), Times.Once);
    }

    [Fact]
    public async Task FunctionHandler_ReportsFailedMessages()
    {
        _s3Client.Setup(x => x.GetObjectAsync(It.IsAny<string>(), It.IsAny<string>(), default))
            .ThrowsAsync(new AmazonS3Exception("Not Found"));

        var sqsEvent = new SQSEvent
        {
            Records = new List<SQSEvent.SQSMessage>
            {
                new()
                {
                    MessageId = "msg-fail",
                    Body = JsonSerializer.Serialize(new S3EventNotification
                    {
                        Records = new List<S3EventRecord>
                        {
                            new() { S3 = new S3Entity { Bucket = new S3BucketEntity { Name = "b" }, Object = new S3ObjectEntity { Key = "k" } } }
                        }
                    })
                }
            }
        };

        var result = await _sut.FunctionHandler(sqsEvent, _context);

        result.BatchItemFailures.Should().HaveCount(1);
        result.BatchItemFailures[0].ItemIdentifier.Should().Be("msg-fail");
    }

    [Theory]
    [InlineData("uploads/user1/abc-uuid/photo.jpg", "abc-uuid")]
    [InlineData("uploads/u/id/file.png", "id")]
    [InlineData("simple.jpg", "simple")]
    public void ExtractAssetIdFromKey_ParsesCorrectly(string key, string expected)
    {
        var result = Function.ExtractAssetIdFromKey(key);
        result.Should().Be(expected);
    }
}
