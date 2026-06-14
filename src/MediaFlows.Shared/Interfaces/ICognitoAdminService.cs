using MediaFlows.Shared.DTOs;
using MediaFlows.Shared.Models.Entities;

namespace MediaFlows.Shared.Interfaces;

public interface ICognitoAdminService
{
    Task<List<CognitoUserDto>> ListUsersAsync(int limit = 50, string? paginationToken = null);
    Task<CognitoUserDto> CreateUserAsync(string email, string displayName, string role, string temporaryPassword);
    Task UpdateUserRoleAsync(string userId, string oldRole, string newRole);
    Task UpdateUserDisplayNameAsync(string userId, string displayName);
    Task DisableUserAsync(string userId);
    Task EnableUserAsync(string userId);
    Task DeleteUserAsync(string userId);
}
