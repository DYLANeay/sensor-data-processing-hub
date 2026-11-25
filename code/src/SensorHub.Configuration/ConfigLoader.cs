using System.Text.Json;

namespace SensorHub.Configuration;

/// <summary>Reads config.json into a <see cref="HubConfig"/> and checks that
/// it describes a usable hub (at least one source and one sink).</summary>
public sealed class ConfigLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public HubConfig Load(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"Config file not found: '{Path.GetFullPath(path)}'.", path);

        HubConfig? config;
        try
        {
            config = JsonSerializer.Deserialize<HubConfig>(File.ReadAllText(path), Options);
        }
        catch (JsonException e)
        {
            // e.Path tells the user where it broke, e.g. "$.sources[0].settings.count".
            throw new InvalidOperationException(
                $"Invalid config file '{path}' at {e.Path}: {e.Message} (setting values must be written as text, e.g. \"factor\": \"1.8\").", e);
        }

        if (config is null)
            throw new InvalidOperationException($"Config file '{path}' is empty.");
        if (config.Sources is not { Count: > 0 })
            throw new InvalidOperationException($"Config file '{path}' must list at least one source.");
        if (config.Sinks is not { Count: > 0 })
            throw new InvalidOperationException($"Config file '{path}' must list at least one sink.");
        if (config.DelayMs < 0)
            throw new InvalidOperationException($"Config file '{path}': delayMs must not be negative, got {config.DelayMs}.");
        config.Transformations ??= [];

        return config;
    }
}
