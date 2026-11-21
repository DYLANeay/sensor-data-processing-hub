using System.Globalization;
using SensorHub.Core;

namespace SensorHub.Processing;

/// <summary>Raises an alert when a reading of <c>sensorType</c> goes above
/// <c>warningThreshold</c>, or a critical alert above <c>criticalThreshold</c>.
/// The reading itself passes through unchanged. Thresholds are compared to
/// the value as it is at this point in the chain, so place this step after
/// any unit conversion and use thresholds in the converted unit.</summary>
public sealed class ThresholdAlerter : ITransformation
{
    private readonly SensorType _sensorType;
    private readonly double _warningThreshold;
    private readonly double _criticalThreshold;

    public ThresholdAlerter(SensorType sensorType, double warningThreshold, double criticalThreshold)
    {
        _sensorType = sensorType;
        _warningThreshold = warningThreshold;
        _criticalThreshold = criticalThreshold;
    }

    public SensorReading Apply(SensorReading reading)
    {
        if (reading.Type != _sensorType) return reading;

        if (reading.Value > _criticalThreshold)
        {
            reading.Alerts.Add(MakeAlert(AlertSeverity.Critical, _criticalThreshold));
        }
        else if (reading.Value > _warningThreshold)
        {
            reading.Alerts.Add(MakeAlert(AlertSeverity.Warning, _warningThreshold));
        }

        return reading;

        // Invariant culture so "25.5" never becomes "25,5" on a Norwegian machine.
        Alert MakeAlert(AlertSeverity severity, double threshold)
        {
            var level = severity.ToString().ToLowerInvariant();
            return new Alert
            {
                Severity = severity,
                Timestamp = DateTime.Now,
                Message = FormattableString.Invariant(
                    $"{reading.SensorId} {reading.Type} {reading.Value:0.##} {reading.Unit} is above {level} {threshold:0.##}"),
            };
        }
    }
}
