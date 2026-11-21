using SensorHub.Core;
using SensorHub.Ingestion;

namespace SensorHub.Tests;

public class RandomSensorSourceTests
{
    [Fact]
    public void Read_YieldsExactlyCountReadings()
    {
        var readings = new RandomSensorSource("sim-01", SensorType.Temperature, "C", 18.0, 24.0, 5).Read().ToList();

        Assert.Equal(5, readings.Count);
    }

    [Fact]
    public void Read_EveryValueLiesWithinConfiguredRange()
    {
        var readings = new RandomSensorSource("sim-01", SensorType.Temperature, "C", 18.0, 24.0, 500).Read().ToList();

        Assert.All(readings, reading => Assert.InRange(reading.Value, 18.0, 24.0));
    }

    [Fact]
    public void Read_UsesConfiguredSensorIdTypeAndUnit()
    {
        var readings = new RandomSensorSource("sim-01", SensorType.CO2, "ppm", 400.0, 800.0, 10).Read().ToList();

        Assert.All(readings, reading =>
        {
            Assert.Equal("sim-01", reading.SensorId);
            Assert.Equal(SensorType.CO2, reading.Type);
            Assert.Equal("ppm", reading.Unit);
        });
    }

    [Fact]
    public void Read_StartsWithEmptyAlerts()
    {
        var readings = new RandomSensorSource("sim-01", SensorType.Temperature, "C", 18.0, 24.0, 3).Read().ToList();

        Assert.All(readings, reading => Assert.Empty(reading.Alerts));
    }
}
