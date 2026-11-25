using SensorHub.Output;
using static SensorHub.Configuration.ConfigSettings;

namespace SensorHub.Configuration;

/// <summary>Turns a sink entry of config.json into an <see cref="IOutputSink"/>.</summary>
public sealed class SinkFactory
{
    public IOutputSink Create(ComponentConfig config) =>
        config.Type?.ToLowerInvariant() switch
        {
            "console" => new ConsoleDashboardSink(),
            "logfile" => new LogFileSink(GetString(config, "path")),
            _ => throw UnknownType(config, "sink", "console", "logFile"),
        };
}
