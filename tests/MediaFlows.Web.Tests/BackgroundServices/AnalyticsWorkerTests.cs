using MediaFlows.Shared.DTOs;
using MediaFlows.Shared.Interfaces;
using MediaFlows.Web.BackgroundServices;
using MediaFlows.Web.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace MediaFlows.Web.Tests.BackgroundServices;

public class AnalyticsWorkerTests
{
    [Fact]
    public async Task ExecuteAsync_StartsAndStopsGracefully()
    {
        var hubMock = new Mock<IHubContext<AnalyticsHub, IAnalyticsClient>>();
        var clientMock = new Mock<IAnalyticsClient>();
        hubMock.Setup(h => h.Clients.All).Returns(clientMock.Object);

        var snapshot = new AnalyticsSnapshotDto { TotalAssets = 42, TotalUsers = 10 };
        var analyticsMock = new Mock<IAnalyticsService>();
        analyticsMock.Setup(a => a.GetCurrentSnapshotAsync()).ReturnsAsync(snapshot);

        var scopeMock = new Mock<IServiceScope>();
        var providerMock = new Mock<IServiceProvider>();
        providerMock.Setup(p => p.GetService(typeof(IAnalyticsService)))
            .Returns(analyticsMock.Object);
        scopeMock.Setup(s => s.ServiceProvider).Returns(providerMock.Object);

        var factoryMock = new Mock<IServiceScopeFactory>();
        factoryMock.Setup(f => f.CreateScope()).Returns(scopeMock.Object);

        var notifHubMock = new Mock<IHubContext<NotificationHub, INotificationClient>>();
        var notifClientMock = new Mock<INotificationClient>();
        notifHubMock.Setup(h => h.Clients.Group(It.IsAny<string>())).Returns(notifClientMock.Object);

        var logger = new Mock<ILogger<AnalyticsWorker>>();

        var worker = new AnalyticsWorker(hubMock.Object, notifHubMock.Object, factoryMock.Object, logger.Object);

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(100));

        await worker.StartAsync(cts.Token);
        await Task.Delay(50);
        await worker.StopAsync(CancellationToken.None);

        logger.Verify(l => l.Log(
            LogLevel.Information,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("started")),
            null,
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_HandlesExceptionsGracefully()
    {
        var hubMock = new Mock<IHubContext<AnalyticsHub, IAnalyticsClient>>();

        var analyticsMock = new Mock<IAnalyticsService>();
        analyticsMock.Setup(a => a.GetCurrentSnapshotAsync())
            .ThrowsAsync(new InvalidOperationException("DB down"));

        var scopeMock = new Mock<IServiceScope>();
        var providerMock = new Mock<IServiceProvider>();
        providerMock.Setup(p => p.GetService(typeof(IAnalyticsService)))
            .Returns(analyticsMock.Object);
        scopeMock.Setup(s => s.ServiceProvider).Returns(providerMock.Object);

        var factoryMock = new Mock<IServiceScopeFactory>();
        factoryMock.Setup(f => f.CreateScope()).Returns(scopeMock.Object);

        var notifHubMock = new Mock<IHubContext<NotificationHub, INotificationClient>>();
        var notifClientMock = new Mock<INotificationClient>();
        notifHubMock.Setup(h => h.Clients.Group(It.IsAny<string>())).Returns(notifClientMock.Object);

        var logger = new Mock<ILogger<AnalyticsWorker>>();

        var worker = new AnalyticsWorker(hubMock.Object, notifHubMock.Object, factoryMock.Object, logger.Object);

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(100));

        // Should not throw even when analytics service throws
        await worker.StartAsync(cts.Token);
        await Task.Delay(50);
        await worker.StopAsync(CancellationToken.None);
    }
}
