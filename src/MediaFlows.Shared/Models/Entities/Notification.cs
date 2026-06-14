using MediaFlows.Shared.Models.Enums;

namespace MediaFlows.Shared.Models.Entities;

public class Notification : IHasTimestamps
{
    public int Id { get; set; }
    public string UserId { get; set; } = null!;
    public NotificationType Type { get; set; }
    public string Title { get; set; } = null!;
    public string Message { get; set; } = null!;
    public string? LinkUrl { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public AppUser User { get; set; } = null!;
}
