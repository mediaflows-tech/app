using FluentAssertions;
using MediaFlows.Web.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;

namespace MediaFlows.Web.Tests.Hubs;

public class AnalyticsHubTests
{
    [Fact]
    public async Task OnConnectedAsync_LogsConnection()
    {
        var logger = new Mock<ILogger<AnalyticsHub>>();
        var hub = new AnalyticsHub(logger.Object);

        var mockContext = new Mock<HubCallerContext>();
        mockContext.Setup(c => c.UserIdentifier).Returns("admin-1");
        mockContext.Setup(c => c.ConnectionId).Returns("conn-789");
        hub.Context = mockContext.Object;

        var mockClients = new Mock<IHubCallerClients<IAnalyticsClient>>();
        hub.Clients = mockClients.Object;

        await hub.OnConnectedAsync();

        logger.Verify(l => l.Log(
            LogLevel.Information,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((v, t) => true),
            null,
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
    }
}
