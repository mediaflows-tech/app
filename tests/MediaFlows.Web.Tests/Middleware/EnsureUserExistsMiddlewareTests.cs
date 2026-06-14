using System.Security.Claims;
using Amazon.CognitoIdentityProvider;
using Amazon.CognitoIdentityProvider.Model;
using FluentAssertions;
using MediaFlows.Data;
using MediaFlows.Shared.Models.Entities;
using MediaFlows.Web.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace MediaFlows.Web.Tests.Middleware;

/// <summary>
/// Test subclass that ignores Npgsql-specific features for InMemory provider.
/// </summary>
internal class TestDbContext : ApplicationDbContext
{
    public TestDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Skip base to avoid Npgsql-specific configurations (TsVector, JSONB ToJson)
        modelBuilder.Entity<AppUser>().HasKey(u => u.CognitoSub);
        modelBuilder.Entity<MediaAsset>().Ignore(a => a.SearchVector);
        modelBuilder.Entity<MediaAsset>().Ignore(a => a.Metadata);
        modelBuilder.Entity<AuditLog>().Ignore(a => a.SearchVector);
        modelBuilder.Entity<MediaAsset>().HasQueryFilter(a => !a.IsDeleted);

        modelBuilder.Entity<AssetVersion>()
            .HasOne(v => v.Asset).WithMany(a => a.Versions).HasForeignKey(v => v.AssetId);
        modelBuilder.Entity<AssetVersion>()
            .HasOne(v => v.UploadedBy).WithMany().HasForeignKey(v => v.UploadedById);
        modelBuilder.Entity<Review>()
            .HasOne(r => r.Asset).WithMany(a => a.Reviews).HasForeignKey(r => r.AssetId);
        modelBuilder.Entity<Review>()
            .HasOne(r => r.Reviewer).WithMany().HasForeignKey(r => r.ReviewerId);
        modelBuilder.Entity<Comment>()
            .HasOne(c => c.Asset).WithMany(a => a.Comments).HasForeignKey(c => c.AssetId);
        modelBuilder.Entity<Comment>()
            .HasOne(c => c.Author).WithMany().HasForeignKey(c => c.AuthorId);
        modelBuilder.Entity<Comment>()
            .HasOne(c => c.ParentComment).WithMany(c => c.Replies).HasForeignKey(c => c.ParentCommentId);
        modelBuilder.Entity<Bookmark>()
            .HasOne(b => b.Asset).WithMany(a => a.Bookmarks).HasForeignKey(b => b.AssetId);
        modelBuilder.Entity<Bookmark>()
            .HasOne(b => b.User).WithMany().HasForeignKey(b => b.UserId);
        modelBuilder.Entity<Notification>()
            .HasOne(n => n.User).WithMany().HasForeignKey(n => n.UserId);
        modelBuilder.Entity<Project>()
            .HasOne(p => p.Owner).WithMany().HasForeignKey(p => p.OwnerId);
        modelBuilder.Entity<MediaAsset>()
            .HasOne(a => a.Creator).WithMany().HasForeignKey(a => a.CreatorId);
        modelBuilder.Entity<MediaAsset>()
            .HasOne(a => a.Project).WithMany(p => p.Assets).HasForeignKey(a => a.ProjectId);
    }
}

public class EnsureUserExistsMiddlewareTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<IAmazonCognitoIdentityProvider> _cognitoMock;
    private readonly ILogger<EnsureUserExistsMiddleware> _logger;
    private bool _nextWasCalled;

    public EnsureUserExistsMiddlewareTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new TestDbContext(options);
        _cognitoMock = new Mock<IAmazonCognitoIdentityProvider>(MockBehavior.Loose);
        _logger = NullLogger<EnsureUserExistsMiddleware>.Instance;
    }

    private Task NextDelegate(HttpContext context)
    {
        _nextWasCalled = true;
        return Task.CompletedTask;
    }

    private Task InvokeMiddleware(EnsureUserExistsMiddleware middleware, HttpContext httpContext)
        => middleware.InvokeAsync(httpContext, _context, _cognitoMock.Object, _logger);

    [Fact]
    public async Task InvokeAsync_AuthenticatedNewUser_CreatesUserInDbFromClaims()
    {
        var middleware = new EnsureUserExistsMiddleware(NextDelegate);
        var httpContext = new DefaultHttpContext();
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim("sub", "new-user-sub"),
            new Claim("email", "new@example.com"),
            new Claim("cognito:username", "newuser"),
            new Claim(ClaimTypes.Role, "ContentCreator"),
        }, "TestAuth"));

        await InvokeMiddleware(middleware, httpContext);

        var user = await _context.AppUsers.FindAsync("new-user-sub");
        user.Should().NotBeNull();
        user!.Email.Should().Be("new@example.com");
        user.DisplayName.Should().Be("newuser");
        user.Role.Should().Be("ContentCreator");
        _nextWasCalled.Should().BeTrue();

        // Claim-based path should not call Cognito
        _cognitoMock.Verify(c => c.GetUserAsync(It.IsAny<GetUserRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task InvokeAsync_NewUserWithoutEmailClaim_FetchesFromCognitoUsingBearerToken()
    {
        _cognitoMock
            .Setup(c => c.GetUserAsync(
                It.Is<GetUserRequest>(r => r.AccessToken == "fake-access-token"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetUserResponse
            {
                UserAttributes = new List<AttributeType>
                {
                    new() { Name = "email", Value = "viewer@example.com" },
                    new() { Name = "name", Value = "Real Name" }
                }
            });

        var middleware = new EnsureUserExistsMiddleware(NextDelegate);
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers.Authorization = "Bearer fake-access-token";
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim("sub", "viewer-sub"),
            new Claim(ClaimTypes.Role, "Viewer"),
        }, "TestAuth"));

        await InvokeMiddleware(middleware, httpContext);

        var user = await _context.AppUsers.FindAsync("viewer-sub");
        user.Should().NotBeNull();
        user!.Email.Should().Be("viewer@example.com");
        user.DisplayName.Should().Be("Real Name");
        user.Role.Should().Be("Viewer");
        _cognitoMock.Verify(c => c.GetUserAsync(It.IsAny<GetUserRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_NewUserWithoutEmailAndCognitoFails_FallsBackToSubAsEmail()
    {
        _cognitoMock
            .Setup(c => c.GetUserAsync(It.IsAny<GetUserRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotAuthorizedException("token expired"));

        var middleware = new EnsureUserExistsMiddleware(NextDelegate);
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers.Authorization = "Bearer expired-token";
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim("sub", "lonely-sub"),
        }, "TestAuth"));

        await InvokeMiddleware(middleware, httpContext);

        var user = await _context.AppUsers.FindAsync("lonely-sub");
        user.Should().NotBeNull();
        // Sub fallback keeps the unique index on Email from collapsing.
        user!.Email.Should().Be("lonely-sub");
        user.DisplayName.Should().Be("lonely-sub");
        _nextWasCalled.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_ExistingUserWithEmptyEmail_BackfillsFromCognito()
    {
        _context.AppUsers.Add(new AppUser
        {
            CognitoSub = "stale-admin-sub",
            Email = "",
            DisplayName = "",
            Role = "SystemAdmin"
        });
        await _context.SaveChangesAsync();

        _cognitoMock
            .Setup(c => c.GetUserAsync(It.IsAny<GetUserRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetUserResponse
            {
                UserAttributes = new List<AttributeType>
                {
                    new() { Name = "email", Value = "admin@example.com" },
                    new() { Name = "name", Value = "Real Admin" }
                }
            });

        var middleware = new EnsureUserExistsMiddleware(NextDelegate);
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers.Authorization = "Bearer some-token";
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim("sub", "stale-admin-sub"),
            new Claim(ClaimTypes.Role, "SystemAdmin"),
        }, "TestAuth"));

        await InvokeMiddleware(middleware, httpContext);

        var user = await _context.AppUsers.FindAsync("stale-admin-sub");
        user.Should().NotBeNull();
        user!.Email.Should().Be("admin@example.com");
        user.DisplayName.Should().Be("Real Admin");
        (await _context.AppUsers.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task InvokeAsync_ExistingUserWithCompleteData_DoesNothing()
    {
        _context.AppUsers.Add(new AppUser
        {
            CognitoSub = "existing-sub",
            Email = "existing@example.com",
            DisplayName = "Existing",
            Role = "Viewer"
        });
        await _context.SaveChangesAsync();

        var middleware = new EnsureUserExistsMiddleware(NextDelegate);
        var httpContext = new DefaultHttpContext();
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim("sub", "existing-sub"),
            new Claim("email", "existing@example.com"),
        }, "TestAuth"));

        await InvokeMiddleware(middleware, httpContext);

        var count = await _context.AppUsers.CountAsync();
        count.Should().Be(1);
        _nextWasCalled.Should().BeTrue();
        _cognitoMock.Verify(c => c.GetUserAsync(It.IsAny<GetUserRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task InvokeAsync_UnauthenticatedUser_SkipsCreation()
    {
        var middleware = new EnsureUserExistsMiddleware(NextDelegate);
        var httpContext = new DefaultHttpContext();
        // User is not authenticated by default

        await InvokeMiddleware(middleware, httpContext);

        var count = await _context.AppUsers.CountAsync();
        count.Should().Be(0);
        _nextWasCalled.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_AlwaysCallsNext()
    {
        var middleware = new EnsureUserExistsMiddleware(NextDelegate);
        var httpContext = new DefaultHttpContext();

        await InvokeMiddleware(middleware, httpContext);

        _nextWasCalled.Should().BeTrue();
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
