using System.Globalization;
using SensorHub.Core;

namespace SensorHub.Output;

/// <summary>Shows a live table on the console: the latest value of every
/// (sensor, type), the alerts active on those latest readings, and the last
/// few alerts raised (so a short spike stays visible). Redrawn after every reading.</summary>
public sealed class ConsoleDashboardSink : IOutputSink
{
    private const int RecentAlertCount = 5;

    private readonly Dictionary<(string, SensorType), SensorReading> _latest = new();
    private readonly Queue<Alert> _recentAlerts = new();

    public void Write(SensorReading reading)
    {
        _latest[(reading.SensorId, reading.Type)] = reading;
        foreach (var alert in reading.Alerts)
        {
            _recentAlerts.Enqueue(alert);
            if (_recentAlerts.Count > RecentAlertCount)
                _recentAlerts.Dequeue(); // drop the oldest
        }
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

        output.WriteLine();
        output.WriteLine("Active alerts:");
        WriteAlerts(output, rows.SelectMany(r => r.Alerts).ToList(), withTime: false);

        output.WriteLine();
        output.WriteLine($"Recent alerts (last {RecentAlertCount}):");
        WriteAlerts(output, _recentAlerts.Reverse().ToList(), withTime: true); // newest first
    }

    private static void WriteAlerts(TextWriter output, List<Alert> alerts, bool withTime)
    {
        if (alerts.Count == 0)
            output.WriteLine("  none");
        foreach (var alert in alerts)
        {
            var time = withTime ? $"{alert.Timestamp:HH:mm:ss}  " : "";
            output.WriteLine($"  {time}[{alert.Severity}] {alert.Message}");
        }
    }
}
