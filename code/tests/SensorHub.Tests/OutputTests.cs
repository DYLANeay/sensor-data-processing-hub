using SensorHub.Core;
using SensorHub.Output;

namespace SensorHub.Tests;

// Same collection as CsvSensorSourceTests: both swap the process-wide Console streams,
// so xunit must not run them at the same time.
[Collection("Console")]
public class OutputTests : IDisposable
{
    private readonly string _logPath = Path.Combine(Path.GetTempPath(), $"sensorhub-{Guid.NewGuid()}", "hub.log");

    public void Dispose()
    {
        var directory = Path.GetDirectoryName(_logPath)!;
        if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
    }

    private static SensorReading Reading(string sensorId, SensorType type, double value, string unit,
        params Alert[] alerts) => new()
    {
        SensorId = sensorId,
        Type = type,
        Value = value,
        Unit = unit,
        Timestamp = new DateTime(2026, 11, 19, 8, 0, 0, DateTimeKind.Utc),
        Alerts = [.. alerts],
    };

    private static Alert Warning(string message) => new() { Severity = AlertSeverity.Warning, Message = message };

    [Fact]
    public void LogFile_WritesOneLinePerReadingWithAlerts()
    {
        var sink = new LogFileSink(_logPath); // directory does not exist yet: the sink creates it
        sink.Write(Reading("room-a", SensorType.Temperature, 75.123, "F", Warning("too hot")));
        sink.Write(Reading("room-a", SensorType.CO2, 610, "ppm"));
        sink.Close();

        var lines = File.ReadAllLines(_logPath);
        Assert.Equal(2, lines.Length);
        Assert.Equal("2026-11-19T08:00:00.0000000Z room-a Temperature 75.12 F | [Warning] too hot", lines[0]);
        Assert.Equal("2026-11-19T08:00:00.0000000Z room-a CO2 610 ppm", lines[1]);
    }

    [Fact]
    public void LogFile_AppendsToExistingFile()
    {
        var first = new LogFileSink(_logPath);
        first.Write(Reading("room-a", SensorType.CO2, 600, "ppm"));
        first.Close();

        var second = new LogFileSink(_logPath);
        second.Write(Reading("room-a", SensorType.CO2, 700, "ppm"));
        second.Close();

        Assert.Equal(2, File.ReadAllLines(_logPath).Length);
    }

    [Fact]
    public void LogFile_WriteAfterCloseThrows()
    {
        var sink = new LogFileSink(_logPath);
        sink.Close();

        Assert.Throws<ObjectDisposedException>(() => sink.Write(Reading("room-a", SensorType.CO2, 600, "ppm")));
    }

    [Fact]
    public void Dashboard_ShowsOnlyLatestReadingPerSensorAndType()
    {
        var output = CaptureConsole(sink =>
        {
            sink.Write(Reading("room-a", SensorType.Temperature, 70, "F"));
            sink.Write(Reading("room-a", SensorType.CO2, 610, "ppm"));
            sink.Write(Reading("room-a", SensorType.Temperature, 72.5, "F"));
        });

        var lastTable = output[output.LastIndexOf("=== Sensor Hub Dashboard ===", StringComparison.Ordinal)..];
        Assert.Contains("72.5", lastTable);
        Assert.DoesNotContain(" 70 ", lastTable); // the old temperature was replaced
        Assert.Contains("610", lastTable);         // CO2 of the same sensor kept its own row
        Assert.Contains("No active alerts.", lastTable);
    }

    [Fact]
    public void Dashboard_ShowsAlertsOfLatestReadings()
    {
        var output = CaptureConsole(sink =>
            sink.Write(Reading("room-a", SensorType.Temperature, 80, "F", Warning("room-a Temperature 80 F is above warning 74"))));

        Assert.Contains("[Warning] room-a Temperature 80 F is above warning 74", output);
    }

    private static string CaptureConsole(Action<ConsoleDashboardSink> act)
    {
        var original = Console.Out;
        var captured = new StringWriter();
        Console.SetOut(captured);
        try
        {
            var sink = new ConsoleDashboardSink();
            act(sink);
            sink.Close();
            return captured.ToString();
        }
        finally
        {
            Console.SetOut(original);
        }
    }
}
