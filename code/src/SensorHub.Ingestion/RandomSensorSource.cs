using System.Runtime.CompilerServices;
using SensorHub.Core;

namespace SensorHub.Ingestion;

/// <summary>Simulates one sensor: a one-hour sine wave around a baseline plus
/// Gaussian noise, producing readings at a fixed interval. With a fixed seed
/// the sequence is deterministic, which makes tests reproducible.</summary>
public sealed class RandomSensorSource(
    string sensorId,
    SensorType type,
    string unit,
    double baseline,
    double amplitude,
    double noiseStdDev,
    TimeSpan interval,
    int? seed = null,
    int? count = null) : ISensorSource
{
    private static readonly TimeSpan WavePeriod = TimeSpan.FromHours(1);

    private readonly Random _random = seed is null ? new Random() : new Random(seed.Value);

    public string Name => $"random:{sensorId}";

    public async IAsyncEnumerable<SensorReading> ReadAsync(
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var start = DateTimeOffset.UtcNow;
        for (var i = 0; count is null || i < count; i++)
        {
            var elapsed = TimeSpan.FromTicks(interval.Ticks * i);
            var value = baseline
                + amplitude * Math.Sin(2.0 * Math.PI * elapsed / WavePeriod)
                + noiseStdDev * NextGaussian();

            yield return new SensorReading(start + elapsed, sensorId, type, value, unit);

            if (count is null || i < count - 1)
            {
                await Task.Delay(interval, ct); // no delay after the last reading
            }
        }
    }

    /// <summary>Standard normal noise via the Box-Muller transform.</summary>
    private double NextGaussian()
    {
        var u1 = 1.0 - _random.NextDouble(); // in (0, 1], so Log is defined
        var u2 = _random.NextDouble();
        return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
    }
}
