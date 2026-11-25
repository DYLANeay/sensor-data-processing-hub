using System.Globalization;
using SensorHub.Core;

namespace SensorHub.Output;

/// <summary>Shows a live table on the console: the latest value of every
/// (sensor, type) and the alerts raised on those latest readings. The table is
/// redrawn after every reading.</summary>
public sealed class ConsoleDashboardSink : IOutputSink
{
    private readonly Dictionary<(string, SensorType), SensorReading> _latest = new();

    public void Write(SensorReading reading)
    {
        _latest[(reading.SensorId, reading.Type)] = reading;
        Render();
    }

    // Nothing to release: the last table stays on screen.
    public void Close() { }

    private void Render()
    {
        // Only clear a real terminal; clearing redirected output (tests, pipes) fails or adds junk.
        if (!Console.IsOutputRedirected)
            Console.Clear();

        var rows = _latest.Values.OrderBy(r => r.SensorId).ThenBy(r => r.Type).ToList();
        var output = Console.Out;
        output.WriteLine("=== Sensor Hub Dashboard ===");
        output.WriteLine($"{"Sensor",-12} {"Type",-12} {"Value",10} {"Unit",-5} Time (UTC)");
        foreach (var r in rows)
            output.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"{r.SensorId,-12} {r.Type,-12} {r.Value,10:0.##} {r.Unit,-5} {r.Timestamp:yyyy-MM-dd HH:mm:ss}"));

        var alerts = rows.SelectMany(r => r.Alerts).ToList();
        output.WriteLine();
        output.WriteLine(alerts.Count == 0 ? "No active alerts." : "Alerts:");
        foreach (var alert in alerts)
            output.WriteLine($"  [{alert.Severity}] {alert.Message}");
    }
}
