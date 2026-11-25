namespace SensorHub.Configuration;

/// <summary>One entry of config.json: the kind of component (e.g. "csv",
/// "unitConverter") and its settings as text. The same shape is used for
/// sources, transformations and sinks, so a new component needs no new config class.</summary>
public sealed class ComponentConfig
{
    public string Type { get; set; } = string.Empty;

    public Dictionary<string, string> Settings { get; set; } = [];
}
