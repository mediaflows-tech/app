using System.ComponentModel.DataAnnotations;
using MediaFlows.Shared.Models.Enums;

namespace MediaFlows.Shared.DTOs;

public class ScheduledPublishDto
{
    public int AssetId { get; set; }
    public string Title { get; set; } = null!;
    public string? ThumbnailUrl { get; set; }
    public DateTime? ScheduledPublishAt { get; set; }
    public AssetStatus Status { get; set; }
}

public class ScheduleUpdateRequest
{
    [Required]
    [Range(1, int.MaxValue)]
    public int AssetId { get; set; }

    [Required]
    public DateTime ScheduledPublishAt { get; set; }
}

public class UnscheduleRequest
{
    [Required]
    [Range(1, int.MaxValue)]
    public int AssetId { get; set; }
}
