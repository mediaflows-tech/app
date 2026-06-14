using System.Security.Claims;
using FluentAssertions;
using MediaFlows.Shared.Interfaces;
using MediaFlows.Web.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;

namespace MediaFlows.Web.Tests.Hubs;

public class NotificationHubTests
{
    [Fact]
    public void INotificationClient_HasExpectedMethods()
    {
        // Verify the interface contract
        var methods = typeof(INotificationClient).GetMethods();
        methods.Should().Contain(m => m.Name == "ReceiveNotification");
        methods.Should().Contain(m => m.Name == "ReceiveToast");
        methods.Should().Contain(m => m.Name == "UpdateBadge");
    }

    [Fact]
    public void IAnalyticsClient_HasExpectedMethods()
    {
        var methods = typeof(IAnalyticsClient).GetMethods();
        methods.Should().Contain(m => m.Name == "ReceiveAnalyticsUpdate");
    }

    [Fact]
    public void NotificationHub_IsAuthorized()
    {
        var attributes = typeof(NotificationHub).GetCustomAttributes(
            typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), true);
        attributes.Should().NotBeEmpty();
    }

    [Fact]
    public void AnalyticsHub_RequiresAdminPolicy()
    {
        var attributes = typeof(AnalyticsHub)
            .GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), true)
            .Cast<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>();

        attributes.Should().Contain(a => a.Policy == "AdminOnly");
    }

    [Fact]
    public void ReceiveToast_HasCorrectParameterTypes()
    {
        var method = typeof(INotificationClient).GetMethod("ReceiveToast");
        method.Should().NotBeNull();

        var parameters = method!.GetParameters();
        parameters.Should().HaveCount(3);
        parameters[0].Name.Should().Be("title");
        parameters[0].ParameterType.Should().Be(typeof(string));
        parameters[1].Name.Should().Be("message");
        parameters[1].ParameterType.Should().Be(typeof(string));
        parameters[2].Name.Should().Be("type");
        parameters[2].ParameterType.Should().Be(typeof(string));
    }

    [Fact]
    public async Task OnConnectedAsync_AddsUserToRoleGroups()
    {
        var logger = new Mock<ILogger<NotificationHub>>();
        var reviewService = new Mock<IReviewService>();
        reviewService.Setup(r => r.GetPendingCountAsync()).ReturnsAsync(7);
        var hub = new NotificationHub(logger.Object, reviewService.Object);

        var mockGroups = new Mock<IGroupManager>();
        var mockContext = new Mock<HubCallerContext>();

        // Production code reads the "sub" claim directly (see NotificationHub
        // comment) — MapInboundClaims=false + NameClaimType=cognito:username
        // means Context.UserIdentifier is null at runtime, so we don't rely
        // on it in the test either.
        var claims = new List<Claim>
        {
            new(ClaimTypes.Role, "Editor"),
            new(ClaimTypes.Role, "ContentCreator"),
            new("sub", "user-1")
        };
        var identity = new ClaimsIdentity(claims, "test");
        var principal = new ClaimsPrincipal(identity);

        mockContext.Setup(c => c.User).Returns(principal);
        mockContext.Setup(c => c.ConnectionId).Returns("conn-123");

        hub.Groups = mockGroups.Object;
        hub.Context = mockContext.Object;

        var mockClients = new Mock<IHubCallerClients<INotificationClient>>();
        var mockCallerClient = new Mock<INotificationClient>();
        mockClients.Setup(c => c.Caller).Returns(mockCallerClient.Object);
        hub.Clients = mockClients.Object;

        await hub.OnConnectedAsync();

        mockGroups.Verify(g => g.AddToGroupAsync("conn-123", "role_Editor", default), Times.Once);
        mockGroups.Verify(g => g.AddToGroupAsync("conn-123", "role_ContentCreator", default), Times.Once);
        mockGroups.Verify(g => g.AddToGroupAsync("conn-123", "user_user-1", default), Times.Once);

        // Editor role → initial pending-review count is pushed to the caller
        mockCallerClient.Verify(
            c => c.UpdateBadge("pending-review-count", "7"),
            Times.Once);
    }

    [Fact]
    public async Task OnConnectedAsync_SkipsPersonalGroup_WhenUserIdIsNull()
    {
        var logger = new Mock<ILogger<NotificationHub>>();
        var reviewService = new Mock<IReviewService>();
        var hub = new NotificationHub(logger.Object, reviewService.Object);

        var mockGroups = new Mock<IGroupManager>();
        var mockContext = new Mock<HubCallerContext>();

        var identity = new ClaimsIdentity(new List<Claim>(), "test");
        var principal = new ClaimsPrincipal(identity);

        mockContext.Setup(c => c.User).Returns(principal);
        mockContext.Setup(c => c.ConnectionId).Returns("conn-456");
        mockContext.Setup(c => c.UserIdentifier).Returns((string?)null);

        hub.Groups = mockGroups.Object;
        hub.Context = mockContext.Object;

        var mockClients = new Mock<IHubCallerClients<INotificationClient>>();
        hub.Clients = mockClients.Object;

        await hub.OnConnectedAsync();

        mockGroups.Verify(g => g.AddToGroupAsync("conn-456",
            It.Is<string>(s => s.StartsWith("role_")), default), Times.Never);
        mockGroups.Verify(g => g.AddToGroupAsync("conn-456",
            It.Is<string>(s => s.StartsWith("user_")), default), Times.Never);

        // No reviewer role → no initial count fetch
        reviewService.Verify(r => r.GetPendingCountAsync(), Times.Never);
    }

    [Fact]
    public async Task OnDisconnectedAsync_LogsDisconnection()
    {
        var logger = new Mock<ILogger<NotificationHub>>();
        var reviewService = new Mock<IReviewService>();
        var hub = new NotificationHub(logger.Object, reviewService.Object);

        var mockContext = new Mock<HubCallerContext>();
        var identity = new ClaimsIdentity(new[] { new Claim("sub", "user-1") }, "test");
        mockContext.Setup(c => c.User).Returns(new ClaimsPrincipal(identity));
        hub.Context = mockContext.Object;

        var mockClients = new Mock<IHubCallerClients<INotificationClient>>();
        hub.Clients = mockClients.Object;

        await hub.OnDisconnectedAsync(null);

        logger.Verify(l => l.Log(
            LogLevel.Information,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("user-1")),
            null,
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
    }
}
