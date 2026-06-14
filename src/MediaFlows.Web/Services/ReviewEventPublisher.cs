using Amazon.EventBridge;
using Amazon.EventBridge.Model;
using MediaFlows.Shared.Configuration;
using MediaFlows.Shared.Interfaces;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace MediaFlows.Web.Services;

public class ReviewEventPublisher : IReviewEventPublisher
{
    private readonly IAmazonEventBridge _eventBridge;
    private readonly string _busName;
    private readonly ILogger<ReviewEventPublisher> _logger;

    public ReviewEventPublisher(
        IAmazonEventBridge eventBridge,
        IOptions<EventBridgeSettings> settings,
        ILogger<ReviewEventPublisher> logger)
    {
        _eventBridge = eventBridge;
        _busName = settings.Value.BusName;
        _logger = logger;
    }

    public async Task PublishCreatorNotificationAsync(string creatorId, string title, string message)
    {
        try
        {
            var detail = JsonSerializer.Serialize(new { creatorId, title, message });

            var response = await _eventBridge.PutEventsAsync(new PutEventsRequest
            {
                Entries = new List<PutEventsRequestEntry>
                {
                    new PutEventsRequestEntry
                    {
                        Source = "mediaflows.reviews",
                        DetailType = title,
                        Detail = detail,
                        EventBusName = _busName
                    }
                }
            });

            if (response.FailedEntryCount > 0)
            {
                _logger.LogWarning(
                    "EventBridge rejected {Count} review notification event(s) for creator {CreatorId}",
                    response.FailedEntryCount, creatorId);
            }
        }
        catch (Exception ex)
        {
            // Best-effort: the in-app notification (DB row + SignalR toast) is
            // already persisted before this call, so a failed event delivery
            // must not break the reviewer's action.
            _logger.LogError(ex,
                "Failed to publish review notification event for creator {CreatorId}", creatorId);
        }
    }
}
