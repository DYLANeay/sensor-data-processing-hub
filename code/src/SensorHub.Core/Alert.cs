namespace SensorHub.Core;

/// <summary>An alert raised on a reading, e.g. a value above a threshold.</summary>
public sealed record Alert(SensorReading Reading, AlertSeverity Severity, string Message);
