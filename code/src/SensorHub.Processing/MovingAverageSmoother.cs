using SensorHub.Core;

namespace SensorHub.Processing;

/// <summary>Replaces each value with the average of the last
/// <c>windowSize</c> values of the same sensor and type.</summary>
public class MovingAverageSmoother : ITransformation
{
    private readonly int _windowSize;
    private readonly Dictionary<string, Queue<double>> _windows = new();

    public MovingAverageSmoother(int windowSize)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(windowSize, 1);
        _windowSize = windowSize;
    }

    public SensorReading Apply(SensorReading reading)
    {
        // Key on id AND type: room-a sends temperature, CO2 and humidity under one id.
        var key = $"{reading.SensorId}/{reading.Type}";
        if (!_windows.TryGetValue(key, out var window))
            _windows[key] = window = new Queue<double>();

        window.Enqueue(reading.Value);
        if (window.Count > _windowSize)
            window.Dequeue();

        reading.Value = window.Average();
        return reading;
    }
}
