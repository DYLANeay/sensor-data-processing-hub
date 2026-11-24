using SensorHub.Core;

namespace SensorHub.Ingestion;

/// <summary>Simulates one sensor: yields exactly <c>count</c> readings with
/// values uniform in [<c>minValue</c>, <c>maxValue</c>].</summary>
public sealed class RandomSensorSource : ISensorSource
{
    private readonly Random _random = new();
    private readonly SensorType _sensorType;
    private readonly int _count;
    private readonly string _sensorId;
    private readonly string _unit;
    private readonly double _minValue;
    private readonly double _maxValue;

    public RandomSensorSource(
        string sensorId, SensorType sensorType, string unit, double minValue, double maxValue, int count)
    {
        if (minValue > maxValue)
            throw new ArgumentException($"minValue ({minValue}) must not be greater than maxValue ({maxValue}).");
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        _sensorId = sensorId;
        _sensorType = sensorType;
        _unit = unit;
        _minValue = minValue;
        _maxValue = maxValue;
        _count = count;
    }

    public IEnumerable<SensorReading> Read()
    {
        for (var i = 0; i < _count; i++)
        {
            yield return new SensorReading
            {
                SensorId = _sensorId,
                Type = _sensorType,
                Value = _minValue + _random.NextDouble() * (_maxValue - _minValue),
                Unit = _unit,
                Timestamp = DateTime.UtcNow,
            };
        }
    }
}
