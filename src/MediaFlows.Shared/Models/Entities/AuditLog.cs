using NpgsqlTypes;

namespace MediaFlows.Shared.Models.Entities;

public class AuditLog
{
    public long Id { get; set; }
    public string Action { get; set; } = null!;
    public string EntityType { get; set; } = null!;
    public string EntityId { get; set; } = null!;
    public string? UserId { get; set; }
    public string? UserEmail { get; set; }
    public string? Details { get; set; }
    public string? IpAddress { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public NpgsqlTsVector SearchVector { get; set; } = null!;
}
