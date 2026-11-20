using SensorHub.Core;
using SensorHub.Ingestion;

namespace SensorHub.Tests;

public class RandomSensorSourceTests
{
    [Fact]
    public void Read_ProducesCountReadingsWithinRange()
    {
        var readings = new RandomSensorSource("sim-01", SensorType.CO2, "ppm", 400, 800, 100).Read().ToList();

        Assert.Equal(100, readings.Count);
        Assert.All(readings, r =>
        {
            Assert.InRange(r.Value, 400, 800);
            Assert.Equal("sim-01", r.SensorId);
            Assert.Equal(SensorType.CO2, r.Type);
            Assert.Equal("ppm", r.Unit);
        });
    }

    [Fact]
    public void EqualMinAndMax_EveryValueEqualsThatValue()
    {
        var readings = new RandomSensorSource("sim-01", SensorType.Temperature, "C", 21, 21, 10).Read();

        Assert.All(readings, r => Assert.Equal(21, r.Value));
    }

    [Fact]
    public void MinAboveMax_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new RandomSensorSource("sim-01", SensorType.Temperature, "C", 25, 20, 10));
    }
}
