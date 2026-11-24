using SensorHub.Core;

namespace SensorHub.Processing;

/// <summary>Linear unit conversion: newValue = value * factor + offset.
/// Celsius to Fahrenheit is (Temperature, "C", "F", 1.8, 32). Readings of any
/// other type or unit pass through unchanged. Because the formula is plain
/// data, a new conversion is a config change, not a new class.</summary>
public sealed class UnitConverter : ITransformation
{
    private readonly SensorType _sensorType;
    private readonly string _fromUnit;
    private readonly string _toUnit;
    private readonly double _factor;
    private readonly double _offset;

    public UnitConverter(SensorType sensorType, string fromUnit, string toUnit, double factor, double offset)
    {
        _sensorType = sensorType;
        _fromUnit = fromUnit;
        _toUnit = toUnit;
        _factor = factor;
        _offset = offset;
    }

    public SensorReading Apply(SensorReading reading)
    {
        if (reading.Type != _sensorType
            || !string.Equals(reading.Unit, _fromUnit, StringComparison.OrdinalIgnoreCase))
            return reading;

        return new SensorReading
        {
            SensorId = reading.SensorId,
            Type = reading.Type,
            Value = reading.Value * _factor + _offset,
            Unit = _toUnit,
            Timestamp = reading.Timestamp,
            Alerts = reading.Alerts,
        };
    }
}
