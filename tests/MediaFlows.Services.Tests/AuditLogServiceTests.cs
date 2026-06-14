using FluentAssertions;
using MediaFlows.Data;
using MediaFlows.Shared.Models.Entities;
using MediaFlows.Tests.Common;
using MediaFlows.Web.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Moq;
using System.Security.Claims;

namespace MediaFlows.Services.Tests;

public class AuditLogServiceTests : IDisposable
{
    private readonly TestDbContext _db;
    private readonly AuditLogService _sut;
    private readonly Mock<IHttpContextAccessor> _httpContextAccessor;

    public AuditLogServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ReplaceService<IModelCacheKeyFactory, TestModelCacheKeyFactory>()
            .Options;
        _db = new TestDbContext(options);

        _httpContextAccessor = new Mock<IHttpContextAccessor>();
        var claims = new List<Claim>
        {
            new("sub", "test-user-id"),
            new("email", "admin@test.com")
        };
        var identity = new ClaimsIdentity(claims, "test");
        var principal = new ClaimsPrincipal(identity);
        var httpContext = new DefaultHttpContext { User = principal };
        _httpContextAccessor.Setup(x => x.HttpContext).Returns(httpContext);

        _sut = new AuditLogService(_db, _httpContextAccessor.Object);
    }

    [Fact]
    public async Task LogAsync_CreatesAuditLogEntry()
    {
        await _sut.LogAsync("User.Create", "AppUser", "42", new { Name = "John" });

        var log = await _db.AuditLogs.FirstOrDefaultAsync();
        log.Should().NotBeNull();
        log!.Action.Should().Be("User.Create");
        log.EntityType.Should().Be("AppUser");
        log.EntityId.Should().Be("42");
        log.UserId.Should().Be("test-user-id");
        log.Details.Should().Contain("John");
    }

    [Fact]
    public async Task SearchAsync_FiltersByActionType()
    {
        _db.AuditLogs.AddRange(
            new AuditLog { Action = "User.Create", EntityType = "AppUser", EntityId = "1", Timestamp = DateTime.UtcNow },
            new AuditLog { Action = "Asset.Upload", EntityType = "MediaAsset", EntityId = "2", Timestamp = DateTime.UtcNow },
            new AuditLog { Action = "User.Create", EntityType = "AppUser", EntityId = "3", Timestamp = DateTime.UtcNow }
        );
        await _db.SaveChangesAsync();

        var result = await _sut.SearchAsync(null, null, "User.Create", null, null, 1, 20);

        result.TotalCount.Should().Be(2);
        result.Items.Should().AllSatisfy(i => i.Action.Should().Be("User.Create"));
    }

    [Fact]
    public async Task SearchAsync_FiltersByDateRange()
    {
        var now = DateTime.UtcNow;
        _db.AuditLogs.AddRange(
            new AuditLog { Action = "Test", EntityType = "X", EntityId = "1", Timestamp = now.AddDays(-5) },
            new AuditLog { Action = "Test", EntityType = "X", EntityId = "2", Timestamp = now.AddDays(-1) },
            new AuditLog { Action = "Test", EntityType = "X", EntityId = "3", Timestamp = now }
        );
        await _db.SaveChangesAsync();

        var result = await _sut.SearchAsync(null, null, null, now.AddDays(-2), now.AddHours(1), 1, 20);

        result.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task SearchAsync_PaginatesCorrectly()
    {
        for (int i = 0; i < 25; i++)
        {
            _db.AuditLogs.Add(new AuditLog
            {
                Action = "Test",
                EntityType = "X",
                EntityId = i.ToString(),
                Timestamp = DateTime.UtcNow.AddMinutes(-i)
            });
        }
        await _db.SaveChangesAsync();

        var page1 = await _sut.SearchAsync(null, null, null, null, null, 1, 10);
        var page2 = await _sut.SearchAsync(null, null, null, null, null, 2, 10);

        page1.Items.Should().HaveCount(10);
        page1.HasMore.Should().BeTrue();
        page2.Items.Should().HaveCount(10);
        page2.HasMore.Should().BeTrue();
    }

    [Fact]
    public async Task GetDistinctActionTypesAsync_ReturnsUniqueActions()
    {
        _db.AuditLogs.AddRange(
            new AuditLog { Action = "User.Create", EntityType = "X", EntityId = "1", Timestamp = DateTime.UtcNow },
            new AuditLog { Action = "User.Create", EntityType = "X", EntityId = "2", Timestamp = DateTime.UtcNow },
            new AuditLog { Action = "Asset.Upload", EntityType = "X", EntityId = "3", Timestamp = DateTime.UtcNow }
        );
        await _db.SaveChangesAsync();

        var result = await _sut.GetDistinctActionTypesAsync();

        result.Should().HaveCount(2);
        result.Should().Contain("User.Create");
        result.Should().Contain("Asset.Upload");
    }

    public void Dispose() => _db.Dispose();
}
