using Amazon.CognitoIdentityProvider;
using Amazon.CognitoIdentityProvider.Model;
using MediaFlows.Data;
using MediaFlows.Shared.Configuration;
using MediaFlows.Shared.DTOs;
using MediaFlows.Shared.Interfaces;
using MediaFlows.Shared.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MediaFlows.Web.Services;

public class CognitoAdminService : ICognitoAdminService
{
    private readonly IAmazonCognitoIdentityProvider _cognito;
    private readonly ApplicationDbContext _db;
    private readonly CognitoSettings _settings;
    private readonly ILogger<CognitoAdminService> _logger;

    public CognitoAdminService(
        IAmazonCognitoIdentityProvider cognito,
        ApplicationDbContext db,
        IOptions<CognitoSettings> settings,
        ILogger<CognitoAdminService> logger)
    {
        _cognito = cognito;
        _db = db;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<List<CognitoUserDto>> ListUsersAsync(int limit = 50, string? paginationToken = null)
    {
        var request = new ListUsersRequest
        {
            UserPoolId = _settings.UserPoolId,
            Limit = limit,
            PaginationToken = paginationToken
        };
        var response = await _cognito.ListUsersAsync(request);
        var users = new List<CognitoUserDto>();
        foreach (var user in response.Users)
        {
            var groups = await _cognito.AdminListGroupsForUserAsync(
                new AdminListGroupsForUserRequest
                {
                    UserPoolId = _settings.UserPoolId,
                    Username = user.Username
                });
            users.Add(new CognitoUserDto
            {
                UserId = user.Attributes.FirstOrDefault(a => a.Name == "sub")?.Value ?? user.Username,
                Username = user.Username,
                Email = user.Attributes.FirstOrDefault(a => a.Name == "email")?.Value ?? "",
                DisplayName = user.Attributes.FirstOrDefault(a => a.Name == "name")?.Value ?? user.Username,
                Role = groups.Groups.FirstOrDefault()?.GroupName ?? "Viewer",
                Status = user.UserStatus.Value,
                Enabled = user.Enabled ?? false,
                CreatedAt = user.UserCreateDate ?? DateTime.MinValue,
                LastModifiedAt = user.UserLastModifiedDate
            });
        }
        return users;
    }

    public async Task<CognitoUserDto> CreateUserAsync(
        string email, string displayName, string role, string temporaryPassword)
    {
        var createRequest = new AdminCreateUserRequest
        {
            UserPoolId = _settings.UserPoolId,
            Username = email,
            TemporaryPassword = temporaryPassword,
            UserAttributes = new List<AttributeType>
            {
                new() { Name = "email", Value = email },
                new() { Name = "email_verified", Value = "true" },
                new() { Name = "name", Value = displayName }
            },
            DesiredDeliveryMediums = new List<string> { "EMAIL" }
        };
        var createResponse = await _cognito.AdminCreateUserAsync(createRequest);
        var cognitoSub = createResponse.User.Attributes
            .FirstOrDefault(a => a.Name == "sub")?.Value ?? "";
        await _cognito.AdminAddUserToGroupAsync(new AdminAddUserToGroupRequest
        {
            UserPoolId = _settings.UserPoolId,
            Username = email,
            GroupName = role
        });
        var appUser = new AppUser
        {
            CognitoSub = cognitoSub,
            Email = email,
            DisplayName = displayName,
            Role = role,
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow
        };
        _db.AppUsers.Add(appUser);
        await _db.SaveChangesAsync();
        _logger.LogInformation("Created user {Email} with role {Role}", email, role);
        return new CognitoUserDto
        {
            UserId = cognitoSub,
            Username = email,
            Email = email,
            DisplayName = displayName,
            Role = role,
            Status = createResponse.User.UserStatus.Value,
            Enabled = createResponse.User.Enabled ?? false,
            CreatedAt = createResponse.User.UserCreateDate ?? DateTime.UtcNow
        };
    }

    public async Task UpdateUserRoleAsync(string userId, string oldRole, string newRole)
    {
        var username = await GetUsernameBySubAsync(userId);
        if (!string.IsNullOrEmpty(oldRole))
        {
            try
            {
                await _cognito.AdminRemoveUserFromGroupAsync(new AdminRemoveUserFromGroupRequest
                {
                    UserPoolId = _settings.UserPoolId,
                    Username = username,
                    GroupName = oldRole
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to remove user {UserId} from group {Group}", userId, oldRole);
            }
        }
        await _cognito.AdminAddUserToGroupAsync(new AdminAddUserToGroupRequest
        {
            UserPoolId = _settings.UserPoolId,
            Username = username,
            GroupName = newRole
        });
        var appUser = await _db.AppUsers.FirstOrDefaultAsync(u => u.CognitoSub == userId);
        if (appUser != null)
        {
            appUser.Role = newRole;
            await _db.SaveChangesAsync();
        }
        _logger.LogInformation("Updated user {UserId} role from {Old} to {New}", userId, oldRole, newRole);
    }

    public async Task DisableUserAsync(string userId)
    {
        var username = await GetUsernameBySubAsync(userId);
        await _cognito.AdminDisableUserAsync(new AdminDisableUserRequest
        {
            UserPoolId = _settings.UserPoolId,
            Username = username
        });
        var appUser = await _db.AppUsers.FirstOrDefaultAsync(u => u.CognitoSub == userId);
        if (appUser != null)
        {
            appUser.IsActive = false;
            await _db.SaveChangesAsync();
        }
        _logger.LogInformation("Disabled user {UserId}", userId);
    }

    public async Task UpdateUserDisplayNameAsync(string userId, string displayName)
    {
        var username = await GetUsernameBySubAsync(userId);
        await _cognito.AdminUpdateUserAttributesAsync(new AdminUpdateUserAttributesRequest
        {
            UserPoolId = _settings.UserPoolId,
            Username = username,
            UserAttributes = new List<AttributeType>
            {
                new() { Name = "name", Value = displayName }
            }
        });
        var appUser = await _db.AppUsers.FirstOrDefaultAsync(u => u.CognitoSub == userId);
        if (appUser != null)
        {
            appUser.DisplayName = displayName;
            await _db.SaveChangesAsync();
        }
        _logger.LogInformation("Updated display name for user {UserId}", userId);
    }

    public async Task EnableUserAsync(string userId)
    {
        var username = await GetUsernameBySubAsync(userId);
        await _cognito.AdminEnableUserAsync(new AdminEnableUserRequest
        {
            UserPoolId = _settings.UserPoolId,
            Username = username
        });
        var appUser = await _db.AppUsers.FirstOrDefaultAsync(u => u.CognitoSub == userId);
        if (appUser != null)
        {
            appUser.IsActive = true;
            await _db.SaveChangesAsync();
        }
        _logger.LogInformation("Enabled user {UserId}", userId);
    }

    public async Task DeleteUserAsync(string userId)
    {
        var username = await GetUsernameBySubAsync(userId);
        await _cognito.AdminDeleteUserAsync(new AdminDeleteUserRequest
        {
            UserPoolId = _settings.UserPoolId,
            Username = username
        });
        var appUser = await _db.AppUsers.FirstOrDefaultAsync(u => u.CognitoSub == userId);
        if (appUser != null)
        {
            _db.AppUsers.Remove(appUser);
            await _db.SaveChangesAsync();
        }
        _logger.LogInformation("Deleted user {UserId}", userId);
    }

    private async Task<string> GetUsernameBySubAsync(string cognitoSub)
    {
        var user = await _db.AppUsers.FirstOrDefaultAsync(u => u.CognitoSub == cognitoSub);
        if (user != null) return user.Email;

        // Fallback: query Cognito directly by sub attribute
        var response = await _cognito.ListUsersAsync(new ListUsersRequest
        {
            UserPoolId = _settings.UserPoolId,
            Filter = $"sub = \"{cognitoSub}\"",
            Limit = 1
        });
        return response.Users.FirstOrDefault()?.Username
            ?? throw new KeyNotFoundException($"User {cognitoSub} not found");
    }
}
