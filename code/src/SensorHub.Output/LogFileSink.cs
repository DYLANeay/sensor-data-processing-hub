using System.Globalization;
using SensorHub.Core;

namespace SensorHub.Output;

/// <summary>Appends one line per reading, with its alerts, to a log file:
/// <c>2026-11-19T08:00:00.0000000Z room-a Temperature 71.96 F | [Warning] ...</c></summary>
public sealed class LogFileSink : IOutputSink
{
    private readonly StreamWriter _writer;

    public LogFileSink(string filePath)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        // Append so earlier runs are kept; AutoFlush so a crash does not lose buffered lines.
        _writer = new StreamWriter(filePath, append: true) { AutoFlush = true };
    }

    public void Write(SensorReading reading)
    {
        var line = string.Create(CultureInfo.InvariantCulture,
            $"{reading.Timestamp:O} {reading.SensorId} {reading.Type} {reading.Value:0.##} {reading.Unit}");
        foreach (var alert in reading.Alerts)
            line += $" | [{alert.Severity}] {alert.Message}";

        _writer.WriteLine(line);
    }

    public void Close() => _writer.Dispose();
}
