namespace MediaFlows.Shared.Interfaces;

/// <summary>
/// Publishes review-domain notification events to EventBridge, which routes
/// them to the NotificationDispatcher Lambda for out-of-band (email) delivery.
/// Implementations are best-effort: a failure here must not break the
/// reviewer's action or the in-app notification.
/// </summary>
public interface IReviewEventPublisher
{
    Task PublishCreatorNotificationAsync(string creatorId, string title, string message);
}
