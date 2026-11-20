namespace SensorHub.Core;

/// <summary>A single measurement, together with the alerts raised on it.</summary>
public class SensorReading
{
    public string SensorId { get; set; } = "";
    public SensorType Type { get; set; }
    public double Value { get; set; }
    public string Unit { get; set; } = "";
    public DateTime Timestamp { get; set; }
    public List<Alert> Alerts { get; set; } = [];
}
