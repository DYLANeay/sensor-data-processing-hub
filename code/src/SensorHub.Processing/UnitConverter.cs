using SensorHub.Core;

namespace SensorHub.Processing;

/// <summary>Linear unit conversion: newValue = value * factor + offset.
/// Celsius to Fahrenheit is ("C", "F", 1.8, 32). Readings in any other unit
/// pass through unchanged. Because the formula is plain data, a new
/// conversion is a config change, not a new class.</summary>
public sealed class UnitConverter(string fromUnit, string toUnit, double factor, double offset = 0)
    : ITransformation
{
    public SensorReading Apply(SensorReading reading, ICollection<Alert> alerts)
    {
        if (!string.Equals(reading.Unit, fromUnit, StringComparison.OrdinalIgnoreCase))
            return reading;

        return reading with { Value = reading.Value * factor + offset, Unit = toUnit };
    }
}
