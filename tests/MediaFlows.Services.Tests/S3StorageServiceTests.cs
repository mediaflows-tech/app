using Amazon.S3;
using Amazon.S3.Model;
using FluentAssertions;
using MediaFlows.Shared.Configuration;
using MediaFlows.Web.Services;
using Microsoft.Extensions.Options;
using Moq;
using System.Net;

namespace MediaFlows.Services.Tests;

public class S3StorageServiceTests
{
    private readonly Mock<IAmazonS3> _s3Client;
    private readonly S3StorageService _sut;

    public S3StorageServiceTests()
    {
        _s3Client = new Mock<IAmazonS3>();
        var settings = Options.Create(new S3Settings
        {
            BucketName = "test-bucket",
            Region = "ap-southeast-1",
            CloudFrontDomain = "d1234.cloudfront.net"
        });
        _sut = new S3StorageService(_s3Client.Object, settings);
    }

    [Fact]
    public void GeneratePresignedPutUrl_CallsS3Client()
    {
        _s3Client.Setup(x => x.GetPreSignedURL(It.IsAny<GetPreSignedUrlRequest>()))
            .Returns("https://presigned-url.com");

        var result = _sut.GeneratePresignedPutUrl("key/test.jpg", "image/jpeg", TimeSpan.FromMinutes(15));

        result.Should().Be("https://presigned-url.com");
        _s3Client.Verify(x => x.GetPreSignedURL(It.Is<GetPreSignedUrlRequest>(r =>
            r.BucketName == "test-bucket" &&
            r.Key == "key/test.jpg" &&
            r.Verb == HttpVerb.PUT &&
            r.ContentType == "image/jpeg")), Times.Once);
    }

    [Fact]
    public async Task ObjectExistsAsync_ReturnsTrueWhenExists()
    {
        _s3Client.Setup(x => x.GetObjectMetadataAsync("test-bucket", "exists.jpg", default))
            .ReturnsAsync(new GetObjectMetadataResponse());

        var result = await _sut.ObjectExistsAsync("exists.jpg");

        result.Should().BeTrue();
    }

    [Fact]
    public async Task ObjectExistsAsync_ReturnsFalseWhenNotFound()
    {
        _s3Client.Setup(x => x.GetObjectMetadataAsync("test-bucket", "missing.jpg", default))
            .ThrowsAsync(new AmazonS3Exception("Not Found") { StatusCode = HttpStatusCode.NotFound });

        var result = await _sut.ObjectExistsAsync("missing.jpg");

        result.Should().BeFalse();
    }

    [Fact]
    public void GetPublicUrl_UsesCloudFrontDomain()
    {
        var url = _sut.GetPublicUrl("thumbnails/1/300x300.webp");

        url.Should().Be("https://d1234.cloudfront.net/thumbnails/1/300x300.webp");
    }

    [Fact]
    public void GetPublicUrl_FallsBackToS3_WhenCloudFrontDomainIsPlaceholder()
    {
        var settings = Options.Create(new S3Settings
        {
            BucketName = "test-bucket",
            Region = "ap-southeast-1",
            CloudFrontDomain = "d1234567890.cloudfront.net"
        });
        var sut = new S3StorageService(_s3Client.Object, settings);

        var url = sut.GetPublicUrl("uploads/user/test.png");

        url.Should().Be("https://test-bucket.s3.ap-southeast-1.amazonaws.com/uploads/user/test.png");
    }

    [Fact]
    public async Task GetObjectMetadataAsync_ReturnsContentTypeAndLength()
    {
        var metadataResponse = new GetObjectMetadataResponse();
        metadataResponse.Headers.ContentType = "image/png";
        metadataResponse.Headers.ContentLength = 4096;

        _s3Client.Setup(x => x.GetObjectMetadataAsync("test-bucket", "images/photo.png", default))
            .ReturnsAsync(metadataResponse);

        var (contentType, contentLength) = await _sut.GetObjectMetadataAsync("images/photo.png");

        contentType.Should().Be("image/png");
        contentLength.Should().Be(4096);
    }

    [Fact]
    public async Task DeleteObjectAsync_CallsS3DeleteObject()
    {
        _s3Client.Setup(x => x.DeleteObjectAsync("test-bucket", "uploads/old.jpg", default))
            .ReturnsAsync(new DeleteObjectResponse());

        await _sut.DeleteObjectAsync("uploads/old.jpg");

        _s3Client.Verify(x => x.DeleteObjectAsync("test-bucket", "uploads/old.jpg", default), Times.Once);
    }

    [Fact]
    public async Task CopyObjectAsync_CopiesWithinSameBucket()
    {
        _s3Client.Setup(x => x.CopyObjectAsync(It.IsAny<CopyObjectRequest>(), default))
            .ReturnsAsync(new CopyObjectResponse());

        await _sut.CopyObjectAsync("source/file.jpg", "dest/file.jpg");

        _s3Client.Verify(x => x.CopyObjectAsync(It.Is<CopyObjectRequest>(r =>
            r.SourceBucket == "test-bucket" &&
            r.SourceKey == "source/file.jpg" &&
            r.DestinationBucket == "test-bucket" &&
            r.DestinationKey == "dest/file.jpg"), default), Times.Once);
    }
}
