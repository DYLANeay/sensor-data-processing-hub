using System.Globalization;
using SensorHub.Core;

namespace SensorHub.Configuration;

/// <summary>Reads typed values out of <see cref="ComponentConfig.Settings"/>
/// with clear error messages. Internal helper shared by the three factories.</summary>
internal static class ConfigSettings
{
    public static string GetString(ComponentConfig config, string key)
    {
        if (config.Settings is null || !config.Settings.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
            throw Error(config, $"missing setting '{key}'");
        return value.Trim();
    }

    public static double GetDouble(ComponentConfig config, string key, double? defaultValue = null)
    {
        if (defaultValue is not null && config.Settings?.ContainsKey(key) != true)
            return defaultValue.Value;

        var text = GetString(config, key);
        // Invariant culture: "1.8" must mean 1.8 on a Norwegian machine too.
        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) || !double.IsFinite(value))
            throw Error(config, $"setting '{key}' must be a number, got '{text}'");
        return value;
    }

    public static int GetInt(ComponentConfig config, string key)
    {
        var text = GetString(config, key);
        if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
            throw Error(config, $"setting '{key}' must be a whole number, got '{text}'");
        return value;
    }

    public static SensorType GetSensorType(ComponentConfig config, string key)
    {
        var text = GetString(config, key);
        // Reject bare digits: Enum.TryParse would accept "1" as CO2.
        if (text.All(char.IsDigit) || !Enum.TryParse<SensorType>(text, ignoreCase: true, out var type) || !Enum.IsDefined(type))
            throw Error(config, $"setting '{key}' must be one of {string.Join(", ", Enum.GetNames<SensorType>())}, got '{text}'");
        return type;
    }

    public static InvalidOperationException UnknownType(ComponentConfig config, string kind, params string[] known) =>
        new($"Unknown {kind} type '{config.Type}'. Known types: {string.Join(", ", known)}.");

    private static InvalidOperationException Error(ComponentConfig config, string problem) =>
        new($"Config entry '{config.Type}': {problem}.");
}
