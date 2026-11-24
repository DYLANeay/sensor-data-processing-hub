namespace SensorHub.Core;

/// <summary>An alert raised on a reading, e.g. a value above a threshold.</summary>
public sealed class Alert
{
    public AlertSeverity Severity { get; set; }

    public string Message { get; set; } = string.Empty;

    public DateTime Timestamp { get; set; }
}
