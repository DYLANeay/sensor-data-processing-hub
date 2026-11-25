using SensorHub.Ingestion;
using static SensorHub.Configuration.ConfigSettings;

namespace SensorHub.Configuration;

/// <summary>Turns a source entry of config.json into an <see cref="ISensorSource"/>.
/// Adding a new kind of source means adding one case here.</summary>
public sealed class SourceFactory
{
    public ISensorSource Create(ComponentConfig config) =>
        config.Type?.ToLowerInvariant() switch
        {
            "csv" => new CsvSensorSource(GetString(config, "path")),
            "random" => new RandomSensorSource(
                GetString(config, "sensorId"),
                GetSensorType(config, "sensorType"),
                GetString(config, "unit"),
                GetDouble(config, "minValue"),
                GetDouble(config, "maxValue"),
                GetInt(config, "count")),
            _ => throw UnknownType(config, "source", "csv", "random"),
        };
}
