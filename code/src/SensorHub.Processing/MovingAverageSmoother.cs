using SensorHub.Core;

namespace SensorHub.Processing;

/// <summary>Replaces each value with the average of the last
/// <c>windowSize</c> values of the same sensor and type, so room-a's
/// temperature is never averaged with room-b's or with a CO2 value.</summary>
public sealed class MovingAverageSmoother : ITransformation
{
    private readonly int _windowSize;
    private readonly Dictionary<(string SensorId, SensorType Type), Queue<double>> _windows = new();

    public MovingAverageSmoother(int windowSize)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(windowSize, 1);
        _windowSize = windowSize;
    }

    public SensorReading Apply(SensorReading reading, ICollection<Alert> alerts)
    {
        var key = (reading.SensorId, reading.Type);
        if (!_windows.TryGetValue(key, out var window))
            _windows[key] = window = new Queue<double>();

        window.Enqueue(reading.Value);
        if (window.Count > _windowSize)
            window.Dequeue();

        return reading with { Value = window.Average() };
    }
}
