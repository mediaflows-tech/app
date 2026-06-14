namespace MediaFlows.Shared.Configuration;

public class EventBridgeSettings
{
    /// <summary>
    /// Name of the custom EventBridge bus that review-domain events are
    /// published to (e.g. <c>mediaflows-events-prod</c>).
    /// </summary>
    public string BusName { get; set; } = string.Empty;
}
