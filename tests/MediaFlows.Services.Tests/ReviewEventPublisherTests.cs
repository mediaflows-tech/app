using Amazon.EventBridge;
using Amazon.EventBridge.Model;
using FluentAssertions;
using MediaFlows.Shared.Configuration;
using MediaFlows.Web.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System.Text.Json;

namespace MediaFlows.Services.Tests;

public class ReviewEventPublisherTests
{
    private static ReviewEventPublisher CreatePublisher(
        Mock<IAmazonEventBridge> eventBridge, string busName = "test-bus")
    {
        var settings = Options.Create(new EventBridgeSettings { BusName = busName });
        var logger = new Mock<ILogger<ReviewEventPublisher>>();
        return new ReviewEventPublisher(eventBridge.Object, settings, logger.Object);
    }

    [Fact]
    public async Task PublishCreatorNotificationAsync_PublishesEventToTheConfiguredBus()
    {
        var eventBridge = new Mock<IAmazonEventBridge>();
        eventBridge.Setup(x => x.PutEventsAsync(It.IsAny<PutEventsRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PutEventsResponse { FailedEntryCount = 0 });

        var publisher = CreatePublisher(eventBridge, busName: "mediaflows-events-test");
        await publisher.PublishCreatorNotificationAsync(
            "creator-1", "Review Decision: Approved", "Your asset was approved.");

        eventBridge.Verify(x => x.PutEventsAsync(It.Is<PutEventsRequest>(r =>
            r.Entries.Count == 1 &&
            r.Entries[0].Source == "mediaflows.reviews" &&
            r.Entries[0].EventBusName == "mediaflows-events-test" &&
            r.Entries[0].DetailType == "Review Decision: Approved"
        ), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PublishCreatorNotificationAsync_IncludesCreatorTitleAndMessageInDetail()
    {
        PutEventsRequest? captured = null;
        var eventBridge = new Mock<IAmazonEventBridge>();
        eventBridge.Setup(x => x.PutEventsAsync(It.IsAny<PutEventsRequest>(), It.IsAny<CancellationToken>()))
            .Callback<PutEventsRequest, CancellationToken>((r, _) => captured = r)
            .ReturnsAsync(new PutEventsResponse { FailedEntryCount = 0 });

        var publisher = CreatePublisher(eventBridge);
        await publisher.PublishCreatorNotificationAsync(
            "creator-9", "Asset Published", "Your asset \"Sunset\" is now live.");

        captured.Should().NotBeNull();
        var detail = JsonSerializer.Deserialize<JsonElement>(captured!.Entries[0].Detail);
        detail.GetProperty("creatorId").GetString().Should().Be("creator-9");
        detail.GetProperty("title").GetString().Should().Be("Asset Published");
        detail.GetProperty("message").GetString().Should().Be("Your asset \"Sunset\" is now live.");
    }

    [Fact]
    public async Task PublishCreatorNotificationAsync_DoesNotThrow_WhenEventBridgeFails()
    {
        var eventBridge = new Mock<IAmazonEventBridge>();
        eventBridge.Setup(x => x.PutEventsAsync(It.IsAny<PutEventsRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AmazonEventBridgeException("EventBridge unavailable"));

        var publisher = CreatePublisher(eventBridge);
        var act = () => publisher.PublishCreatorNotificationAsync(
            "creator-1", "Review Decision: Rejected", "Your asset was rejected.");

        await act.Should().NotThrowAsync();
    }
}
