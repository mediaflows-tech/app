using MediaFlows.Shared.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MediaFlows.Web.Controllers.Api;

[Route("api/v1/admin/users")]
[Authorize(Policy = "AdminOnly")]
public class AdminUsersApiController : ApiBaseController
{
    private readonly ICognitoAdminService _cognitoAdmin;
    private readonly IAuditLogService _auditLog;

    public AdminUsersApiController(ICognitoAdminService cognitoAdmin, IAuditLogService auditLog)
    {
        _cognitoAdmin = cognitoAdmin;
        _auditLog = auditLog;
    }

    [HttpGet("")]
    public async Task<IActionResult> ListUsers(string? role = null)
    {
        var users = await _cognitoAdmin.ListUsersAsync();
        if (!string.IsNullOrEmpty(role))
            users = users.Where(u => u.Role == role).ToList();
        return Ok(users);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetUser(string id)
    {
        var users = await _cognitoAdmin.ListUsersAsync();
        var user = users.FirstOrDefault(u => u.UserId == id);
        if (user == null)
            return ApiNotFound("User");
        return Ok(user);
    }

    [HttpPost("")]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);
        try
        {
            var user = await _cognitoAdmin.CreateUserAsync(
                request.Email, request.DisplayName, request.Role, request.TemporaryPassword);
            await _auditLog.LogAsync("User.Create", "AppUser", user.UserId,
                new { request.Email, request.Role });
            return Ok(user);
        }
        catch (Exception ex)
        {
            return ApiError($"Failed to create user: {ex.Message}");
        }
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateUser(string id, [FromBody] UpdateUserRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);
        try
        {
            await _cognitoAdmin.UpdateUserDisplayNameAsync(id, request.DisplayName);
            await _auditLog.LogAsync("User.Update", "AppUser", id,
                new { request.DisplayName });
            if (!string.IsNullOrEmpty(request.CurrentRole) && request.Role != request.CurrentRole)
            {
                await _cognitoAdmin.UpdateUserRoleAsync(id, request.CurrentRole, request.Role);
                await _auditLog.LogAsync("User.RoleChange", "AppUser", id,
                    new { OldRole = request.CurrentRole, NewRole = request.Role });
            }
            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            return ApiError($"Failed to update user: {ex.Message}");
        }
    }

    [HttpPost("{id}/disable")]
    public async Task<IActionResult> DisableUser(string id)
    {
        try
        {
            await _cognitoAdmin.DisableUserAsync(id);
            await _auditLog.LogAsync("User.Disable", "AppUser", id);
            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            return ApiError($"Failed to disable user: {ex.Message}");
        }
    }

    [HttpPost("{id}/enable")]
    public async Task<IActionResult> EnableUser(string id)
    {
        try
        {
            await _cognitoAdmin.EnableUserAsync(id);
            await _auditLog.LogAsync("User.Enable", "AppUser", id);
            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            return ApiError($"Failed to enable user: {ex.Message}");
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteUser(string id)
    {
        try
        {
            await _cognitoAdmin.DeleteUserAsync(id);
            await _auditLog.LogAsync("User.Delete", "AppUser", id);
            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            return ApiError($"Failed to delete user: {ex.Message}");
        }
    }
}

public record CreateUserRequest(string Email, string DisplayName, string Role, string TemporaryPassword);
public record UpdateUserRequest(string DisplayName, string Role, string? CurrentRole);
