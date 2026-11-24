using System.Globalization;
using SensorHub.Core;
using SensorHub.Processing;

namespace SensorHub.Tests;

public class ProcessingTests
{
    private static SensorReading Reading(double value, string unit = "C",
        SensorType type = SensorType.Temperature, string sensorId = "room-a") =>
        new()
        {
            SensorId = sensorId,
            Type = type,
            Value = value,
            Unit = unit,
            Timestamp = new DateTime(2026, 11, 19, 8, 0, 0, DateTimeKind.Utc),
        };

    [Fact]
    public void UnitConverter_ConvertsMatchingTypeAndUnit()
    {
        var result = new UnitConverter(SensorType.Temperature, "C", "F", 1.8, 32).Apply(Reading(100));

        Assert.Equal(212, result.Value, precision: 6);
        Assert.Equal("F", result.Unit);
        Assert.Equal("room-a", result.SensorId);
        Assert.Equal(SensorType.Temperature, result.Type);
        Assert.Equal(Reading(0).Timestamp, result.Timestamp);
    }

    [Fact]
    public void UnitConverter_LeavesOtherTypesAndUnitsUnchanged()
    {
        var converter = new UnitConverter(SensorType.Temperature, "C", "F", 1.8, 32);

        // A CO2 reading in ppm passes through even though a Temperature C->F
        // converter is configured, and so does a Temperature in another unit.
        var co2 = Reading(610, "ppm", SensorType.CO2);
        Assert.Same(co2, converter.Apply(co2));

        var kelvin = Reading(294.15, "K");
        Assert.Same(kelvin, converter.Apply(kelvin));
    }

    [Fact]
    public void Smoother_AveragesLastNValuesPerSensor()
    {
        var smoother = new MovingAverageSmoother(windowSize: 2);

        Assert.Equal(10, smoother.Apply(Reading(10)).Value);
        Assert.Equal(15, smoother.Apply(Reading(20)).Value);
        // A different sensor gets its own window and does not disturb room-a.
        Assert.Equal(100, smoother.Apply(Reading(100, sensorId: "room-b")).Value);
        Assert.Equal(25, smoother.Apply(Reading(30)).Value); // (20 + 30) / 2, 10 dropped
    }

    [Fact]
    public void Smoother_ReturnsNewInstanceWithCopiedProperties()
    {
        var input = Reading(21);
        input.Alerts.Add(new Alert { Severity = AlertSeverity.Warning, Message = "already there" });

        var result = new MovingAverageSmoother(3).Apply(input);

        Assert.NotSame(input, result);
        Assert.Equal(21, result.Value);
        Assert.Equal(input.SensorId, result.SensorId);
        Assert.Equal(input.Type, result.Type);
        Assert.Equal(input.Unit, result.Unit);
        Assert.Equal(input.Timestamp, result.Timestamp);
        Assert.Same(input.Alerts, result.Alerts);
    }

    [Fact]
    public void Smoother_RejectsWindowBelowOne()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new MovingAverageSmoother(0));
    }

    [Theory]
    [InlineData(1001, AlertSeverity.Critical)]
    [InlineData(1000, AlertSeverity.Warning)] // exactly on critical: only above warning
    [InlineData(600, AlertSeverity.Warning)]
    [InlineData(400, null)] // exactly on the warning threshold is fine
    [InlineData(300, null)]
    public void ThresholdAlerter_AlertsAboveThresholds(double value, AlertSeverity? expectedSeverity)
    {
        var alerter = new ThresholdAlerter(SensorType.CO2, warningThreshold: 400, criticalThreshold: 1000);
        var reading = Reading(value, "ppm", SensorType.CO2);

        var result = alerter.Apply(reading);

        Assert.Same(reading, result); // reading passes through unchanged
        Assert.Equal(value, result.Value);
        Assert.Equal(expectedSeverity is null ? 0 : 1, reading.Alerts.Count);
        if (expectedSeverity is not null)
            Assert.Equal(expectedSeverity, reading.Alerts.Single().Severity);
    }

    [Fact]
    public void ThresholdAlerter_RejectsWarningAboveCritical()
    {
        Assert.Throws<ArgumentException>(() =>
            new ThresholdAlerter(SensorType.Temperature, warningThreshold: 80, criticalThreshold: 70));
    }

    [Fact]
    public void ThresholdAlerter_IgnoresOtherSensorTypes()
    {
        var alerter = new ThresholdAlerter(SensorType.CO2, warningThreshold: 400, criticalThreshold: 1000);
        var reading = Reading(5000, "C", SensorType.Temperature); // way above any CO2 threshold

        var result = alerter.Apply(reading);

        Assert.Same(reading, result);
        Assert.Empty(reading.Alerts);
    }

    [Fact]
    public void ThresholdAlerter_MessageIsCultureInvariant()
    {
        var original = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("nb-NO");
        try
        {
            var reading = Reading(26.25);
            new ThresholdAlerter(SensorType.Temperature, warningThreshold: 25, criticalThreshold: 25.5)
                .Apply(reading);

            Assert.Equal("room-a Temperature 26.25 C is above critical 25.5", reading.Alerts.Single().Message);
            Assert.Equal(AlertSeverity.Critical, reading.Alerts.Single().Severity);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void Chain_RunsStepsInOrderAndCollectsAlerts()
    {
        // Convert to F first, then alert above 80 F: 30 C = 86 F -> Warning.
        var chain = new TransformationChain();
        chain.Add(new UnitConverter(SensorType.Temperature, "C", "F", 1.8, 32));
        chain.Add(new ThresholdAlerter(SensorType.Temperature, warningThreshold: 80, criticalThreshold: 90));

        var reading = chain.Apply(Reading(30));

        Assert.Equal(86, reading.Value, precision: 6);
        Assert.Equal("F", reading.Unit);
        var alert = Assert.Single(reading.Alerts);
        Assert.Equal(AlertSeverity.Warning, alert.Severity);
    }

    [Fact]
    public void Chain_WithNoStepsReturnsReadingUnchanged()
    {
        var input = Reading(21);
        var reading = new TransformationChain().Apply(input);

        Assert.Same(input, reading);
        Assert.Empty(reading.Alerts);
    }
}
