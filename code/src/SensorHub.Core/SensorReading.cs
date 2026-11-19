namespace SensorHub.Core;

/// <summary>One immutable reading from a sensor.</summary>
public sealed record SensorReading(DateTimeOffset Timestamp, string SensorId, SensorType Type, double Value, string Unit);
