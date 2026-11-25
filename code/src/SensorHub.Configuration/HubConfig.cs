namespace SensorHub.Configuration;

/// <summary>The whole parsed config.json: which sources to read, the
/// transformation chain in order, and which sinks to write to.</summary>
public sealed class HubConfig
{
    public List<ComponentConfig> Sources { get; set; } = [];

    public List<ComponentConfig> Transformations { get; set; } = [];

    public List<ComponentConfig> Sinks { get; set; } = [];

    /// <summary>Pause after each reading, in milliseconds (0 = as fast as possible).</summary>
    public int DelayMs { get; set; }
}
