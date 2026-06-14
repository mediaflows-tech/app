using Amazon.CognitoIdentityProvider;
using Amazon.CognitoIdentityProvider.Model;
using FluentAssertions;
using MediaFlows.Data;
using MediaFlows.Shared.Configuration;
using MediaFlows.Shared.Models.Entities;
using MediaFlows.Tests.Common;
using MediaFlows.Web.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace MediaFlows.Services.Tests;

public class CognitoAdminServiceTests : IDisposable
{
    private readonly TestDbContext _db;
    private readonly Mock<IAmazonCognitoIdentityProvider> _cognito;
    private readonly CognitoAdminService _sut;
    private readonly CognitoSettings _settings;

    public CognitoAdminServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ReplaceService<IModelCacheKeyFactory, TestModelCacheKeyFactory>()
            .Options;
        _db = new TestDbContext(options);
        _cognito = new Mock<IAmazonCognitoIdentityProvider>();
        _settings = new CognitoSettings { UserPoolId = "ap-southeast-1_TestPool" };
        var settingsOptions = Options.Create(_settings);
        var logger = new Mock<ILogger<CognitoAdminService>>();
        _sut = new CognitoAdminService(_cognito.Object, _db, settingsOptions, logger.Object);
    }

    [Fact]
    public async Task CreateUserAsync_CreatesInCognitoAndPostgres()
    {
        _cognito.Setup(x => x.AdminCreateUserAsync(It.IsAny<AdminCreateUserRequest>(), default))
            .ReturnsAsync(new AdminCreateUserResponse
            {
                User = new UserType
                {
                    Username = "test@example.com",
                    UserStatus = UserStatusType.FORCE_CHANGE_PASSWORD,
                    Enabled = true,
                    UserCreateDate = DateTime.UtcNow,
                    Attributes = new List<AttributeType>
                    {
                        new() { Name = "sub", Value = "cognito-sub-123" },
                        new() { Name = "email", Value = "test@example.com" }
                    }
                }
            });
        _cognito.Setup(x => x.AdminAddUserToGroupAsync(It.IsAny<AdminAddUserToGroupRequest>(), default))
            .ReturnsAsync(new AdminAddUserToGroupResponse());

        var result = await _sut.CreateUserAsync("test@example.com", "Test User", "ContentCreator", "TempPass123!");

        result.Email.Should().Be("test@example.com");
        result.Role.Should().Be("ContentCreator");
        result.UserId.Should().Be("cognito-sub-123");

        var dbUser = await _db.AppUsers.FirstOrDefaultAsync(u => u.CognitoSub == "cognito-sub-123");
        dbUser.Should().NotBeNull();
        dbUser!.Email.Should().Be("test@example.com");
        dbUser.Role.Should().Be("ContentCreator");

        _cognito.Verify(x => x.AdminCreateUserAsync(
            It.Is<AdminCreateUserRequest>(r => r.Username == "test@example.com"), default), Times.Once);
        _cognito.Verify(x => x.AdminAddUserToGroupAsync(
            It.Is<AdminAddUserToGroupRequest>(r => r.GroupName == "ContentCreator"), default), Times.Once);
    }

    [Fact]
    public async Task DisableUserAsync_DisablesInCognitoAndPostgres()
    {
        _db.AppUsers.Add(new AppUser
        {
            CognitoSub = "sub-456",
            Email = "user@test.com",
            DisplayName = "User",
            Role = "Viewer",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        _cognito.Setup(x => x.AdminDisableUserAsync(It.IsAny<AdminDisableUserRequest>(), default))
            .ReturnsAsync(new AdminDisableUserResponse());

        await _sut.DisableUserAsync("sub-456");

        var dbUser = await _db.AppUsers.FirstAsync(u => u.CognitoSub == "sub-456");
        dbUser.IsActive.Should().BeFalse();
        _cognito.Verify(x => x.AdminDisableUserAsync(
            It.Is<AdminDisableUserRequest>(r => r.Username == "user@test.com"), default), Times.Once);
    }

    [Fact]
    public async Task UpdateUserRoleAsync_ChangesGroupAndSyncsDb()
    {
        _db.AppUsers.Add(new AppUser
        {
            CognitoSub = "sub-789",
            Email = "editor@test.com",
            DisplayName = "Editor",
            Role = "Editor",
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        _cognito.Setup(x => x.AdminRemoveUserFromGroupAsync(It.IsAny<AdminRemoveUserFromGroupRequest>(), default))
            .ReturnsAsync(new AdminRemoveUserFromGroupResponse());
        _cognito.Setup(x => x.AdminAddUserToGroupAsync(It.IsAny<AdminAddUserToGroupRequest>(), default))
            .ReturnsAsync(new AdminAddUserToGroupResponse());

        await _sut.UpdateUserRoleAsync("sub-789", "Editor", "SystemAdmin");

        var dbUser = await _db.AppUsers.FirstAsync(u => u.CognitoSub == "sub-789");
        dbUser.Role.Should().Be("SystemAdmin");
        _cognito.Verify(x => x.AdminRemoveUserFromGroupAsync(
            It.Is<AdminRemoveUserFromGroupRequest>(r => r.GroupName == "Editor"), default), Times.Once);
        _cognito.Verify(x => x.AdminAddUserToGroupAsync(
            It.Is<AdminAddUserToGroupRequest>(r => r.GroupName == "SystemAdmin"), default), Times.Once);
    }

    [Fact]
    public async Task DeleteUserAsync_RemovesFromCognitoAndPostgres()
    {
        _db.AppUsers.Add(new AppUser
        {
            CognitoSub = "sub-delete",
            Email = "delete@test.com",
            DisplayName = "Delete Me",
            Role = "Viewer",
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        _cognito.Setup(x => x.AdminDeleteUserAsync(It.IsAny<AdminDeleteUserRequest>(), default))
            .ReturnsAsync(new AdminDeleteUserResponse());

        await _sut.DeleteUserAsync("sub-delete");

        var dbUser = await _db.AppUsers.FirstOrDefaultAsync(u => u.CognitoSub == "sub-delete");
        dbUser.Should().BeNull();
    }

    [Fact]
    public async Task ListUsersAsync_ReturnsMappedUsers()
    {
        _cognito.Setup(x => x.ListUsersAsync(It.IsAny<ListUsersRequest>(), default))
            .ReturnsAsync(new ListUsersResponse
            {
                Users = new List<UserType>
                {
                    new()
                    {
                        Username = "user1@test.com",
                        Enabled = true,
                        UserStatus = UserStatusType.CONFIRMED,
                        UserCreateDate = DateTime.UtcNow,
                        Attributes = new List<AttributeType>
                        {
                            new() { Name = "sub", Value = "sub-1" },
                            new() { Name = "email", Value = "user1@test.com" },
                            new() { Name = "name", Value = "User One" }
                        }
                    }
                }
            });
        _cognito.Setup(x => x.AdminListGroupsForUserAsync(It.IsAny<AdminListGroupsForUserRequest>(), default))
            .ReturnsAsync(new AdminListGroupsForUserResponse
            {
                Groups = new List<GroupType> { new() { GroupName = "Viewer" } }
            });

        var result = await _sut.ListUsersAsync();

        result.Should().HaveCount(1);
        result.First().UserId.Should().Be("sub-1");
        result.First().Email.Should().Be("user1@test.com");
    }

    [Fact]
    public async Task EnableUserAsync_EnablesInCognitoAndPostgres()
    {
        _db.AppUsers.Add(new AppUser
        {
            CognitoSub = "sub-enable",
            Email = "enable@test.com",
            DisplayName = "Enable Me",
            Role = "Viewer",
            IsActive = false,
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        _cognito.Setup(x => x.AdminEnableUserAsync(It.IsAny<AdminEnableUserRequest>(), default))
            .ReturnsAsync(new AdminEnableUserResponse());

        await _sut.EnableUserAsync("sub-enable");

        var dbUser = await _db.AppUsers.FirstAsync(u => u.CognitoSub == "sub-enable");
        dbUser.IsActive.Should().BeTrue();
        _cognito.Verify(x => x.AdminEnableUserAsync(
            It.Is<AdminEnableUserRequest>(r => r.Username == "enable@test.com"), default), Times.Once);
    }

    public void Dispose() => _db.Dispose();
}
