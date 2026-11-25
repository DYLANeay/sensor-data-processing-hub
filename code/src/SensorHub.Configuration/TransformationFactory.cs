using SensorHub.Processing;
using static SensorHub.Configuration.ConfigSettings;

namespace SensorHub.Configuration;

/// <summary>Turns transformation entries of config.json into the steps of the
/// chain. The order in the file is the order in the chain.</summary>
public sealed class TransformationFactory
{
    public ITransformation Create(ComponentConfig config) =>
        config.Type?.ToLowerInvariant() switch
        {
            "unitconverter" => new UnitConverter(
                GetSensorType(config, "sensorType"),
                GetString(config, "fromUnit"),
                GetString(config, "toUnit"),
                GetDouble(config, "factor"),
                GetDouble(config, "offset", defaultValue: 0)),
            "movingaverage" => new MovingAverageSmoother(GetInt(config, "windowSize")),
            "threshold" => new ThresholdAlerter(
                GetSensorType(config, "sensorType"),
                GetDouble(config, "warningThreshold"),
                GetDouble(config, "criticalThreshold")),
            _ => throw UnknownType(config, "transformation", "unitConverter", "movingAverage", "threshold"),
        };

    public TransformationChain CreateChain(List<ComponentConfig> configs)
    {
        var chain = new TransformationChain();
        foreach (var config in configs)
            chain.Add(Create(config));
        return chain;
    }
}
