namespace SensorHub.Core;

/// <summary>An alert raised on a reading, e.g. a value above a threshold.</summary>
public class Alert
{
    public AlertSeverity Severity { get; set; }
    public string Message { get; set; } = "";
    public DateTime Timestamp { get; set; }
}
