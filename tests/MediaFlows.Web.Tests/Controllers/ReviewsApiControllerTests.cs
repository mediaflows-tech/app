using System.Security.Claims;
using FluentAssertions;
using MediaFlows.Shared.DTOs;
using MediaFlows.Shared.Interfaces;
using MediaFlows.Web.Controllers.Api;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace MediaFlows.Web.Tests.Controllers;

public class ReviewsApiControllerTests
{
    private readonly Mock<IReviewService> _reviewService = new();
    private readonly Mock<IS3StorageService> _s3 = new();

    private ReviewsApiController BuildSut()
    {
        var sut = new ReviewsApiController(_reviewService.Object, _s3.Object);
        sut.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    new[] { new Claim("sub", "reviewer-1") }, "test"))
            }
        };
        return sut;
    }

    [Fact]
    public async Task BatchPublish_ReturnsProcessedAndSkippedCounts()
    {
        _reviewService
            .Setup(s => s.BatchApproveAndPublishAsync(
                It.IsAny<int[]>(), "reviewer-1", It.IsAny<string?>()))
            .ReturnsAsync(2);

        var result = await BuildSut().BatchPublish(new BatchPublishRequest
        {
            AssetIds = new[] { 1, 2, 3 }
        });

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeEquivalentTo(
            new { success = true, count = 2, skipped = 1 },
            opts => opts.ExcludingMissingMembers());
    }
}
