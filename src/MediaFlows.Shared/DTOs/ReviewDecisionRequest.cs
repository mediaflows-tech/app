using System.ComponentModel.DataAnnotations;
using MediaFlows.Shared.Models.Enums;

namespace MediaFlows.Shared.DTOs;

public class ReviewDecisionRequest
{
    [Required]
    public int AssetId { get; set; }

    [Required]
    public ReviewDecision Decision { get; set; }

    [MaxLength(2000)]
    public string? Comments { get; set; }

    public DateTime? ScheduledPublishAt { get; set; }

    public bool PublishImmediately { get; set; }
}

public class BatchReviewDecisionRequest
{
    [Required]
    [MinLength(1)]
    public int[] AssetIds { get; set; } = Array.Empty<int>();

    [Required]
    public ReviewDecision Decision { get; set; }

    [MaxLength(2000)]
    public string? Comments { get; set; }
}

public class RejectApprovedRequest
{
    [MaxLength(2000)]
    public string? Comments { get; set; }
}

public class BatchPublishRequest
{
    [Required]
    [MinLength(1)]
    public int[] AssetIds { get; set; } = Array.Empty<int>();

    [MaxLength(2000)]
    public string? Comments { get; set; }
}

public class BatchScheduleRequest
{
    [Required]
    [MinLength(1)]
    public int[] AssetIds { get; set; } = Array.Empty<int>();

    [Required]
    public DateTime ScheduledPublishAt { get; set; }
}
