using MediaFlows.Shared.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace MediaFlows.Web.Hubs;

public interface INotificationClient
{
    Task ReceiveNotification(string htmlFragment);
    Task ReceiveToast(string title, string message, string type);
    Task UpdateBadge(string elementId, string value);
}

[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class NotificationHub : Hub<INotificationClient>
{
    private readonly ILogger<NotificationHub> _logger;
    private readonly IReviewService _reviewService;

    public NotificationHub(ILogger<NotificationHub> logger, IReviewService reviewService)
    {
        _logger = logger;
        _reviewService = reviewService;
    }

    public override async Task OnConnectedAsync()
    {
        // Auto-join role-based groups
        var roles = Context.User?.Claims
            .Where(c => c.Type == ClaimTypes.Role)
            .Select(c => c.Value)
            .ToList() ?? new List<string>();

        foreach (var role in roles)
            await Groups.AddToGroupAsync(Context.ConnectionId, $"role_{role}");

        // Join personal group for user-specific notifications.
        // Read the Cognito `sub` claim directly: the JWT pipeline runs with
        // MapInboundClaims=false and only overrides NameClaimType, so neither
        // ClaimTypes.NameIdentifier nor Context.UserIdentifier is populated.
        // Using "sub" here matches ApiBaseController.CurrentUserId and the
        // creatorId value stored on every MediaAsset row.
        var userId = Context.User?.FindFirstValue("sub");
        if (!string.IsNullOrEmpty(userId))
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{userId}");

        // Reviewers (Editor / SystemAdmin) get their current pending-review count
        // pushed immediately so the sidebar badge is populated before any decision
        // event fires. Keeps the flow one-way (server → client) and avoids a
        // separate REST endpoint. Wrapped in try/catch so a transient DB error
        // doesn't take down the whole SignalR handshake — the next decision
        // broadcast will still refresh the badge.
        if (roles.Any(r => r is "Editor" or "SystemAdmin"))
        {
            try
            {
                var pendingCount = await _reviewService.GetPendingCountAsync();
                await Clients.Caller.UpdateBadge("pending-review-count", pendingCount.ToString());
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to push initial pending-review count to {UserId}", userId);
            }
        }

        _logger.LogInformation("User {UserId} connected to NotificationHub", userId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = Context.User?.FindFirstValue("sub");
        _logger.LogInformation("User {UserId} disconnected from NotificationHub", userId);
        await base.OnDisconnectedAsync(exception);
    }
}
