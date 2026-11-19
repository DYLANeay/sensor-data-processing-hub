using SensorHub.Core;
using SensorHub.Ingestion;

namespace SensorHub.Tests;

public class RandomSensorSourceTests
{
    [Fact]
    public async Task SameSeed_ProducesIdenticalValueSequences()
    {
        RandomSensorSource MakeSource() => new(
            "sim-01", SensorType.Temperature, "C",
            baseline: 21.0, amplitude: 1.5, noiseStdDev: 0.3,
            interval: TimeSpan.Zero, seed: 42, count: 10);

        var first = await ReadValues(MakeSource());
        var second = await ReadValues(MakeSource());

        Assert.Equal(first, second);
    }

    [Fact]
    public async Task ZeroNoiseAndAmplitude_EveryValueEqualsBaseline()
    {
        var values = await ReadValues(new RandomSensorSource(
            "sim-01", SensorType.Temperature, "C",
            baseline: 21.0, amplitude: 0.0, noiseStdDev: 0.0,
            interval: TimeSpan.Zero, seed: 42, count: 10));

        Assert.All(values, value => Assert.Equal(21.0, value));
    }

    private static async Task<List<double>> ReadValues(RandomSensorSource source)
    {
        var values = new List<double>();
        await foreach (var reading in source.ReadAsync())
            values.Add(reading.Value);
        return values;
    }
}
