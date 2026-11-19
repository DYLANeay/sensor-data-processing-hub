using System.Globalization;
using SensorHub.Core;

namespace SensorHub.Processing;

/// <summary>Raises an alert when a reading of <c>type</c> goes below
/// <c>min</c> or above <c>max</c> (either bound can be left out). The reading
/// itself passes through unchanged. Thresholds are compared to the value as it
/// is at this point in the chain, so place this step after any unit
/// conversion and use thresholds in the converted unit.</summary>
public sealed class ThresholdAlerter(SensorType type, double? min, double? max, AlertSeverity severity)
    : ITransformation
{
    public SensorReading Apply(SensorReading reading, ICollection<Alert> alerts)
    {
        if (reading.Type != type) return reading;

        string? problem = null;
        if (max is not null && reading.Value > max) problem = Invariant($"above max {max}");
        else if (min is not null && reading.Value < min) problem = Invariant($"below min {min}");

        if (problem is not null)
        {
            var message = Invariant(
                $"{reading.SensorId} {reading.Type} {reading.Value:0.##} {reading.Unit} is {problem}");
            alerts.Add(new Alert(reading, severity, message));
        }

        return reading;
    }

    // Invariant culture so "25.5" never becomes "25,5" on a Norwegian machine.
    private static string Invariant(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);
}
