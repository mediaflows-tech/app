using Amazon.Rekognition;
using Amazon.Rekognition.Model;
using FluentAssertions;
using MediaFlows.Shared.Configuration;
using MediaFlows.Web.Services;
using Microsoft.Extensions.Options;
using Moq;

namespace MediaFlows.Services.Tests;

public class RekognitionServiceTests
{
    private readonly Mock<IAmazonRekognition> _rekognition;
    private readonly RekognitionService _sut;

    public RekognitionServiceTests()
    {
        _rekognition = new Mock<IAmazonRekognition>();
        var settings = Options.Create(new RekognitionSettings
        {
            Enabled = true,
            BucketName = "test-bucket",
            AssetUploadedTopicArn = "arn:aws:sns:us-east-1:123:test"
        });
        _sut = new RekognitionService(_rekognition.Object, settings);
    }

    [Fact]
    public async Task DetectLabelsAsync_MapsResponseToAssetTags()
    {
        _rekognition.Setup(x => x.DetectLabelsAsync(It.IsAny<DetectLabelsRequest>(), default))
            .ReturnsAsync(new DetectLabelsResponse
            {
                Labels = new List<Label>
                {
                    new() { Name = "Landscape", Confidence = 98.5f },
                    new() { Name = "Nature", Confidence = 90.0f },
                    new() { Name = "Sky", Confidence = 85.0f }
                }
            });

        var result = await _sut.DetectLabelsAsync("uploads/photo.jpg");

        result.Should().HaveCount(3);
        result[0].Name.Should().Be("Landscape");
        result[0].Confidence.Should().Be(98.5f);
        result[2].Name.Should().Be("Sky");

        _rekognition.Verify(x => x.DetectLabelsAsync(
            It.Is<DetectLabelsRequest>(r =>
                r.Image.S3Object.Bucket == "test-bucket" &&
                r.Image.S3Object.Name == "uploads/photo.jpg" &&
                r.MaxLabels == 20), default), Times.Once);
    }

    [Fact]
    public async Task DetectModerationLabelsAsync_SafeContent_ReturnsIsSafeTrue()
    {
        _rekognition.Setup(x => x.DetectModerationLabelsAsync(It.IsAny<DetectModerationLabelsRequest>(), default))
            .ReturnsAsync(new DetectModerationLabelsResponse
            {
                ModerationLabels = new List<ModerationLabel>()
            });

        var result = await _sut.DetectModerationLabelsAsync("uploads/safe.jpg");

        result.IsSafe.Should().BeTrue();
        result.Labels.Should().BeEmpty();
        result.ScannedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task DetectModerationLabelsAsync_UnsafeContent_ReturnsIsSafeFalse()
    {
        _rekognition.Setup(x => x.DetectModerationLabelsAsync(It.IsAny<DetectModerationLabelsRequest>(), default))
            .ReturnsAsync(new DetectModerationLabelsResponse
            {
                ModerationLabels = new List<ModerationLabel>
                {
                    new() { Name = "Explicit Nudity", ParentName = "Nudity", Confidence = 92.0f }
                }
            });

        var result = await _sut.DetectModerationLabelsAsync("uploads/flagged.jpg");

        result.IsSafe.Should().BeFalse();
        result.Labels.Should().HaveCount(1);
        result.Labels[0].Name.Should().Be("Explicit Nudity");
        result.Labels[0].ParentName.Should().Be("Nudity");
        result.Labels[0].Confidence.Should().Be(92.0f);
    }
}
