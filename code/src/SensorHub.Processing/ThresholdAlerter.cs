using System.Globalization;
using SensorHub.Core;

namespace SensorHub.Processing;

/// <summary>Adds a Critical alert to readings of one sensor type above
/// <c>criticalThreshold</c>, or a Warning alert above <c>warningThreshold</c>.
/// The value itself is not changed. Thresholds are compared to the value as it
/// is at this point in the chain, so place this step after any unit
/// conversion and use thresholds in the converted unit.</summary>
public class ThresholdAlerter : ITransformation
{
    private readonly SensorType _sensorType;
    private readonly double _warningThreshold;
    private readonly double _criticalThreshold;

    public ThresholdAlerter(SensorType sensorType, double warningThreshold, double criticalThreshold)
    {
        if (warningThreshold > criticalThreshold)
            throw new ArgumentException(
                $"warningThreshold ({warningThreshold}) must not be greater than criticalThreshold ({criticalThreshold}).");

        _sensorType = sensorType;
        _warningThreshold = warningThreshold;
        _criticalThreshold = criticalThreshold;
    }

    public SensorReading Apply(SensorReading reading)
    {
        if (reading.Type != _sensorType) return reading;

        AlertSeverity severity;
        double threshold;
        if (reading.Value > _criticalThreshold) (severity, threshold) = (AlertSeverity.Critical, _criticalThreshold);
        else if (reading.Value > _warningThreshold) (severity, threshold) = (AlertSeverity.Warning, _warningThreshold);
        else return reading;

        reading.Alerts.Add(new Alert
        {
            Severity = severity,
            // Invariant culture so "25.5" never becomes "25,5" on a Norwegian machine.
            Message = string.Create(CultureInfo.InvariantCulture,
                $"{reading.SensorId} {reading.Type} {reading.Value:0.##} {reading.Unit} is above {severity} threshold {threshold}"),
            Timestamp = reading.Timestamp,
        });
        return reading;
    }
}
