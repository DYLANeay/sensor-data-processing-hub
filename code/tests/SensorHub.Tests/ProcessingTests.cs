using System.Globalization;
using SensorHub.Core;
using SensorHub.Processing;

namespace SensorHub.Tests;

public class ProcessingTests
{
    private static SensorReading Reading(double value, string unit = "C",
        SensorType type = SensorType.Temperature, string sensorId = "room-a") =>
        new(DateTimeOffset.UnixEpoch, sensorId, type, value, unit);

    [Fact]
    public void UnitConverter_CelsiusToFahrenheit()
    {
        var result = new UnitConverter("C", "F", 1.8, 32).Apply(Reading(100), new List<Alert>());

        Assert.Equal(212, result.Value, precision: 6);
        Assert.Equal("F", result.Unit);
    }

    [Fact]
    public void UnitConverter_LeavesOtherUnitsUnchanged()
    {
        var co2 = Reading(610, "ppm", SensorType.CO2);

        Assert.Equal(co2, new UnitConverter("C", "F", 1.8, 32).Apply(co2, new List<Alert>()));
    }

    [Fact]
    public void Smoother_AveragesLastNValuesPerSensor()
    {
        var smoother = new MovingAverageSmoother(windowSize: 2);
        var alerts = new List<Alert>();

        Assert.Equal(10, smoother.Apply(Reading(10), alerts).Value);
        Assert.Equal(15, smoother.Apply(Reading(20), alerts).Value);
        // A different sensor gets its own window and does not disturb room-a.
        Assert.Equal(100, smoother.Apply(Reading(100, sensorId: "room-b"), alerts).Value);
        Assert.Equal(25, smoother.Apply(Reading(30), alerts).Value); // (20 + 30) / 2, 10 dropped
    }

    [Fact]
    public void Smoother_RejectsWindowBelowOne()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new MovingAverageSmoother(0));
    }

    [Theory]
    [InlineData(1001, true)]
    [InlineData(1000, false)] // exactly on the threshold is fine
    [InlineData(300, true)]
    [InlineData(600, false)]
    public void ThresholdAlerter_AlertsOutsideMinMax(double value, bool expectAlert)
    {
        var alerter = new ThresholdAlerter(SensorType.CO2, min: 400, max: 1000, AlertSeverity.Warning);
        var alerts = new List<Alert>();

        var result = alerter.Apply(Reading(value, "ppm", SensorType.CO2), alerts);

        Assert.Equal(value, result.Value); // reading passes through unchanged
        Assert.Equal(expectAlert ? 1 : 0, alerts.Count);
    }

    [Fact]
    public void ThresholdAlerter_IgnoresOtherSensorTypes()
    {
        var alerts = new List<Alert>();
        new ThresholdAlerter(SensorType.CO2, null, 1000, AlertSeverity.Warning).Apply(Reading(5000), alerts);

        Assert.Empty(alerts);
    }

    [Fact]
    public void ThresholdAlerter_MessageIsCultureInvariant()
    {
        var original = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("nb-NO");
        try
        {
            var alerts = new List<Alert>();
            new ThresholdAlerter(SensorType.Temperature, null, 25.5, AlertSeverity.Critical)
                .Apply(Reading(26.25), alerts);

            Assert.Equal("room-a Temperature 26.25 C is above max 25.5", alerts.Single().Message);
            Assert.Equal(AlertSeverity.Critical, alerts.Single().Severity);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void Chain_RunsStepsInOrderAndCollectsAlerts()
    {
        // Convert to F first, then alert above 80 F: 30 C = 86 F.
        var chain = new TransformationChain(new ITransformation[]
        {
            new UnitConverter("C", "F", 1.8, 32),
            new ThresholdAlerter(SensorType.Temperature, null, 80, AlertSeverity.Warning),
        });

        var (reading, alerts) = chain.Process(Reading(30));

        Assert.Equal(86, reading.Value, precision: 6);
        Assert.Equal("F", reading.Unit);
        Assert.Single(alerts);
    }

    [Fact]
    public void Chain_WithNoStepsReturnsReadingUnchanged()
    {
        var input = Reading(21);
        var (reading, alerts) = new TransformationChain([]).Process(input);

        Assert.Equal(input, reading);
        Assert.Empty(alerts);
    }
}
