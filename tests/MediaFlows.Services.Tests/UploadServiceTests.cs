using Amazon.S3;
using Amazon.S3.Model;
using FluentAssertions;
using MediaFlows.Shared.Configuration;
using MediaFlows.Shared.DTOs;
using MediaFlows.Shared.Interfaces;
using MediaFlows.Shared.Models.ValueObjects;
using MediaFlows.Web.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System.Net;

namespace MediaFlows.Services.Tests;

public class UploadServiceTests
{
    private readonly Mock<IAmazonS3> _s3Client;
    private readonly Mock<IRekognitionService> _rekognition;
    private readonly UploadService _sut;
    private readonly S3Settings _s3Settings;
    private readonly RekognitionSettings _rekognitionSettings;
    private readonly Mock<ILogger<UploadService>> _logger;

    public UploadServiceTests()
    {
        _s3Client = new Mock<IAmazonS3>();
        _rekognition = new Mock<IRekognitionService>();
        _s3Settings = new S3Settings
        {
            BucketName = "test-bucket",
            Region = "ap-southeast-1"
        };
        _rekognitionSettings = new RekognitionSettings
        {
            Enabled = false,
            BucketName = "test-bucket"
        };
        _logger = new Mock<ILogger<UploadService>>();

        _s3Client.Setup(x => x.GetPreSignedURL(It.IsAny<GetPreSignedUrlRequest>()))
            .Returns("https://test-bucket.s3.amazonaws.com/presigned-url");

        _sut = CreateService();
    }

    private UploadService CreateService(bool rekognitionEnabled = false)
    {
        _rekognitionSettings.Enabled = rekognitionEnabled;
        return new UploadService(
            _s3Client.Object,
            Options.Create(_s3Settings),
            _rekognition.Object,
            Options.Create(_rekognitionSettings),
            _logger.Object);
    }

    [Fact]
    public void GeneratePresignedUrl_ReturnsValidResponse()
    {
        var result = _sut.GeneratePresignedUrl("user-123", "photo.jpg", "image/jpeg");

        result.UploadUrl.Should().NotBeNullOrEmpty();
        result.S3Key.Should().StartWith("uploads/user-123/");
        result.S3Key.Should().EndWith("/photo.jpg");
        result.ExpiresAt.Should().BeAfter(DateTime.UtcNow);
    }

    [Fact]
    public void GeneratePresignedUrl_SanitizesFileName()
    {
        var result = _sut.GeneratePresignedUrl("user-123", "my file (1).jpg", "image/jpeg");

        result.S3Key.Should().Contain("my file (1).jpg");
    }

    [Fact]
    public void GeneratePresignedUrl_RejectsInvalidContentType()
    {
        var act = () => _sut.GeneratePresignedUrl("user-123", "malware.exe", "application/x-executable");
        act.Should().Throw<ArgumentException>().WithMessage("*not allowed*");
    }

    [Theory]
    [InlineData("image/jpeg")]
    [InlineData("image/png")]
    [InlineData("image/webp")]
    [InlineData("image/gif")]
    [InlineData("image/svg+xml")]
    [InlineData("image/bmp")]
    [InlineData("image/tiff")]
    [InlineData("video/mp4")]
    [InlineData("video/quicktime")]
    [InlineData("video/webm")]
    [InlineData("video/x-msvideo")]
    [InlineData("video/x-matroska")]
    [InlineData("audio/mpeg")]
    [InlineData("audio/wav")]
    [InlineData("audio/flac")]
    [InlineData("audio/x-flac")]
    [InlineData("audio/aac")]
    [InlineData("audio/ogg")]
    [InlineData("application/pdf")]
    public void GeneratePresignedUrl_AcceptsAllowedTypes(string contentType)
    {
        var result = _sut.GeneratePresignedUrl("user-123", "file.test", contentType);

        result.UploadUrl.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ConfirmUploadAsync_ReturnsMediaAsset()
    {
        _s3Client.Setup(x => x.GetObjectMetadataAsync("test-bucket", "uploads/u1/uuid/photo.jpg", default))
            .ReturnsAsync(new GetObjectMetadataResponse
            {
                Headers = { ContentType = "image/jpeg", ContentLength = 1024000 }
            });

        var request = new UploadConfirmRequest
        {
            S3Key = "uploads/u1/uuid/photo.jpg",
            FileName = "photo.jpg",
            ContentType = "image/jpeg",
            FileSize = 1024000
        };

        var asset = await _sut.ConfirmUploadAsync(request, "user-123");

        asset.CreatorId.Should().Be("user-123");
        asset.Title.Should().Be("photo");
        asset.S3Key.Should().Be("uploads/u1/uuid/photo.jpg");
        asset.ContentType.Should().Be("image/jpeg");
        asset.FileSize.Should().Be(1024000);
        asset.Status.Should().Be(MediaFlows.Shared.Models.Enums.AssetStatus.Draft);
        asset.Metadata.Format.Should().Be("jpg");
    }

    [Fact]
    public async Task ConfirmUploadAsync_WhenRekognitionEnabledAndImageIsSafe_ReturnsAssetWithModerationMetadata()
    {
        var sut = CreateService(rekognitionEnabled: true);
        _s3Client.Setup(x => x.GetObjectMetadataAsync("test-bucket", "uploads/u1/uuid/photo.jpg", default))
            .ReturnsAsync(new GetObjectMetadataResponse
            {
                Headers = { ContentType = "image/jpeg", ContentLength = 1024000 }
            });
        _rekognition.Setup(x => x.DetectLabelsAsync("uploads/u1/uuid/photo.jpg", 75f))
            .ReturnsAsync(new List<AssetTag>
            {
                new() { Name = "Portrait", Confidence = 98.2f }
            });
        _rekognition.Setup(x => x.DetectModerationLabelsAsync("uploads/u1/uuid/photo.jpg"))
            .ReturnsAsync(new ModerationResult
            {
                IsSafe = true,
                ScannedAt = DateTime.UtcNow,
                Labels = new List<MediaFlows.Shared.Models.ValueObjects.ModerationLabel>()
            });

        var request = new UploadConfirmRequest
        {
            S3Key = "uploads/u1/uuid/photo.jpg",
            FileName = "photo.jpg",
            ContentType = "image/jpeg",
            FileSize = 1024000
        };

        var asset = await sut.ConfirmUploadAsync(request, "user-123");

        asset.Metadata.AutoTags.Should().ContainSingle(t => t.Name == "Portrait" && t.Confidence == 98.2f);
        asset.Metadata.Moderation.Should().NotBeNull();
        asset.Metadata.Moderation!.IsSafe.Should().BeTrue();
        _s3Client.Verify(x => x.DeleteObjectAsync(It.IsAny<string>(), It.IsAny<string>(), default), Times.Never);
    }

    [Fact]
    public async Task ConfirmUploadAsync_WhenRekognitionEnabledAndImageIsUnsafe_DeletesUploadAndRejectsAsset()
    {
        var sut = CreateService(rekognitionEnabled: true);
        _s3Client.Setup(x => x.GetObjectMetadataAsync("test-bucket", "uploads/u1/uuid/nsfw.jpg", default))
            .ReturnsAsync(new GetObjectMetadataResponse
            {
                Headers = { ContentType = "image/jpeg", ContentLength = 1024000 }
            });
        _rekognition.Setup(x => x.DetectLabelsAsync("uploads/u1/uuid/nsfw.jpg", 75f))
            .ReturnsAsync(new List<AssetTag>());
        _rekognition.Setup(x => x.DetectModerationLabelsAsync("uploads/u1/uuid/nsfw.jpg"))
            .ReturnsAsync(new ModerationResult
            {
                IsSafe = false,
                ScannedAt = DateTime.UtcNow,
                Labels = new List<MediaFlows.Shared.Models.ValueObjects.ModerationLabel>
                {
                    new() { Name = "Explicit Nudity", ParentName = "Nudity", Confidence = 92.5f }
                }
            });
        _s3Client.Setup(x => x.DeleteObjectAsync("test-bucket", "uploads/u1/uuid/nsfw.jpg", default))
            .ReturnsAsync(new DeleteObjectResponse());

        var request = new UploadConfirmRequest
        {
            S3Key = "uploads/u1/uuid/nsfw.jpg",
            FileName = "nsfw.jpg",
            ContentType = "image/jpeg",
            FileSize = 1024000
        };

        var act = async () => await sut.ConfirmUploadAsync(request, "user-123");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*content moderation*");
        _s3Client.Verify(x => x.DeleteObjectAsync("test-bucket", "uploads/u1/uuid/nsfw.jpg", default), Times.Once);
    }

    [Fact]
    public async Task ConfirmUploadAsync_WhenRekognitionEnabledAndImageTypeUnsupported_SkipsModerationAndReturnsAsset()
    {
        var sut = CreateService(rekognitionEnabled: true);
        _s3Client.Setup(x => x.GetObjectMetadataAsync("test-bucket", "uploads/u1/uuid/animated.gif", default))
            .ReturnsAsync(new GetObjectMetadataResponse
            {
                Headers = { ContentType = "image/gif", ContentLength = 1024000 }
            });

        var request = new UploadConfirmRequest
        {
            S3Key = "uploads/u1/uuid/animated.gif",
            FileName = "animated.gif",
            ContentType = "image/gif",
            FileSize = 1024000
        };

        var asset = await sut.ConfirmUploadAsync(request, "user-123");

        asset.ContentType.Should().Be("image/gif");
        asset.Metadata.Format.Should().Be("gif");
        asset.Metadata.AutoTags.Should().BeEmpty();
        asset.Metadata.Moderation.Should().BeNull();
        _rekognition.Verify(x => x.DetectLabelsAsync(It.IsAny<string>(), It.IsAny<float>()), Times.Never);
        _rekognition.Verify(x => x.DetectModerationLabelsAsync(It.IsAny<string>()), Times.Never);
        _s3Client.Verify(x => x.DeleteObjectAsync(It.IsAny<string>(), It.IsAny<string>(), default), Times.Never);
    }

    [Fact]
    public async Task ConfirmUploadAsync_ThrowsWhenObjectNotFound()
    {
        _s3Client.Setup(x => x.GetObjectMetadataAsync("test-bucket", "nonexistent", default))
            .ThrowsAsync(new AmazonS3Exception("Not Found") { StatusCode = HttpStatusCode.NotFound });

        var request = new UploadConfirmRequest { S3Key = "nonexistent", FileName = "x.jpg" };

        var act = async () => await _sut.ConfirmUploadAsync(request, "user-123");
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*not found*");
    }

    [Theory]
    [InlineData("normal.jpg", "normal.jpg")]
    [InlineData("", "unnamed")]
    [InlineData("   ", "unnamed")]
    public void SanitizeFileName_HandlesEdgeCases(string input, string expected)
    {
        var result = UploadService.SanitizeFileName(input);

        result.Should().Be(expected);
    }

    [Fact]
    public void SanitizeFileName_TruncatesLongNames()
    {
        var longName = new string('a', 300) + ".jpg";

        var result = UploadService.SanitizeFileName(longName);

        result.Length.Should().BeLessThanOrEqualTo(200);
        result.Should().EndWith(".jpg");
    }
}
