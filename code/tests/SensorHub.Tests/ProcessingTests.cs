using System.Globalization;
using SensorHub.Core;
using SensorHub.Processing;

namespace SensorHub.Tests;

public class ProcessingTests
{
    private static SensorReading Reading(double value, string unit = "C",
        SensorType type = SensorType.Temperature, string sensorId = "room-a") =>
        new() { SensorId = sensorId, Type = type, Value = value, Unit = unit, Timestamp = DateTime.UnixEpoch };

    private static UnitConverter CelsiusToFahrenheit() => new(SensorType.Temperature, "C", "F", 1.8, 32);

    [Fact]
    public void UnitConverter_CelsiusToFahrenheit()
    {
        var result = CelsiusToFahrenheit().Apply(Reading(100));

        Assert.Equal(212, result.Value, precision: 6);
        Assert.Equal("F", result.Unit);
    }

    [Fact]
    public void UnitConverter_LeavesOtherUnitsAndTypesUnchanged()
    {
        var co2 = CelsiusToFahrenheit().Apply(Reading(610, "ppm", SensorType.CO2));
        var alreadyF = CelsiusToFahrenheit().Apply(Reading(70, "F"));
        var wrongType = CelsiusToFahrenheit().Apply(Reading(40, "C", SensorType.Humidity));

        Assert.Equal((610, "ppm"), (co2.Value, co2.Unit));
        Assert.Equal((70, "F"), (alreadyF.Value, alreadyF.Unit));
        Assert.Equal((40, "C"), (wrongType.Value, wrongType.Unit));
    }

    [Fact]
    public void Smoother_AveragesLastNValuesPerSensorAndType()
    {
        var smoother = new MovingAverageSmoother(windowSize: 2);

        Assert.Equal(10, smoother.Apply(Reading(10)).Value);
        Assert.Equal(15, smoother.Apply(Reading(20)).Value);
        // Other sensors, and other types under the same id, get their own window.
        Assert.Equal(100, smoother.Apply(Reading(100, sensorId: "room-b")).Value);
        Assert.Equal(446, smoother.Apply(Reading(446, "ppm", SensorType.CO2)).Value);
        Assert.Equal(25, smoother.Apply(Reading(30)).Value); // (20 + 30) / 2, 10 dropped
    }

    [Fact]
    public void Smoother_RejectsWindowBelowOne()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new MovingAverageSmoother(0));
    }

    [Theory]
    [InlineData(600, null)]
    [InlineData(1000, null)] // exactly on the warning threshold is fine
    [InlineData(1001, AlertSeverity.Warning)]
    [InlineData(2000, AlertSeverity.Warning)]
    [InlineData(2001, AlertSeverity.Critical)]
    public void ThresholdAlerter_PicksSeverityFromThresholds(double value, AlertSeverity? expected)
    {
        var alerter = new ThresholdAlerter(SensorType.CO2, warningThreshold: 1000, criticalThreshold: 2000);

        var result = alerter.Apply(Reading(value, "ppm", SensorType.CO2));

        Assert.Equal(value, result.Value); // value passes through unchanged
        Assert.Equal(expected, result.Alerts.SingleOrDefault()?.Severity);
    }

    [Fact]
    public void ThresholdAlerter_IgnoresOtherSensorTypes()
    {
        var result = new ThresholdAlerter(SensorType.CO2, 1000, 2000).Apply(Reading(5000));

        Assert.Empty(result.Alerts);
    }

    [Fact]
    public void ThresholdAlerter_RejectsWarningAboveCritical()
    {
        Assert.Throws<ArgumentException>(() => new ThresholdAlerter(SensorType.CO2, 2000, 1000));
    }

    [Fact]
    public void ThresholdAlerter_AlertHasCultureInvariantMessageAndReadingTimestamp()
    {
        var original = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("nb-NO");
        try
        {
            var alert = new ThresholdAlerter(SensorType.Temperature, 25.5, 30)
                .Apply(Reading(26.25)).Alerts.Single();

            Assert.Equal("room-a Temperature 26.25 C is above Warning threshold 25.5", alert.Message);
            Assert.Equal(DateTime.UnixEpoch, alert.Timestamp);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void Chain_RunsStepsInOrderAndKeepsAlertsOnReading()
    {
        // Convert to F first, then alert above 80 F: 30 C = 86 F.
        var chain = new TransformationChain();
        chain.Add(CelsiusToFahrenheit());
        chain.Add(new ThresholdAlerter(SensorType.Temperature, 80, 90));

        var reading = chain.Apply(Reading(30));

        Assert.Equal(86, reading.Value, precision: 6);
        Assert.Equal("F", reading.Unit);
        Assert.Equal(AlertSeverity.Warning, reading.Alerts.Single().Severity);
    }

    [Fact]
    public void Chain_IsItselfATransformation()
    {
        // Composite: a chain can be a step inside another chain.
        var inner = new TransformationChain();
        inner.Add(CelsiusToFahrenheit());
        var outer = new TransformationChain();
        outer.Add(inner);

        Assert.Equal(212, outer.Apply(Reading(100)).Value, precision: 6);
    }

    [Fact]
    public void EmptyChain_ReturnsReadingUnchanged()
    {
        var result = new TransformationChain().Apply(Reading(21));

        Assert.Equal(21, result.Value);
        Assert.Empty(result.Alerts);
    }
}
