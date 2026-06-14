using MediaFlows.Shared.DTOs;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace MediaFlows.Web.Hubs;

public interface IAnalyticsClient
{
    Task ReceiveAnalyticsUpdate(AnalyticsSnapshotDto data);
}

[Authorize(
    AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme,
    Policy = "AdminOnly")]
public class AnalyticsHub : Hub<IAnalyticsClient>
{
    private readonly ILogger<AnalyticsHub> _logger;

    public AnalyticsHub(ILogger<AnalyticsHub> logger)
    {
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Admin connected to AnalyticsHub: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }
}
