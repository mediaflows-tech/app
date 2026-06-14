using MediaFlows.Shared.Models.Enums;

namespace MediaFlows.Shared.Models.Entities;

public class AppUser : IHasTimestamps
{
    public string CognitoSub { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string DisplayName { get; set; } = null!;
    public string Role { get; set; } = "Viewer";
    public string? AvatarUrl { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime LastLoginAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
