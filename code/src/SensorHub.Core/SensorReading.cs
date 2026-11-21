namespace SensorHub.Core;

/// <summary>One reading from a sensor, together with the alerts raised on it.</summary>
public sealed class SensorReading
{
    public string SensorId { get; set; } = string.Empty;

    public SensorType Type { get; set; }

    public double Value { get; set; }

    public string Unit { get; set; } = string.Empty;

    public DateTime Timestamp { get; set; }

    public List<Alert> Alerts { get; set; } = [];
}
