using System.Globalization;
using System.Runtime.CompilerServices;
using SensorHub.Core;

namespace SensorHub.Ingestion;

/// <summary>Streams readings from a CSV file with the fixed header
/// "timestamp,sensor_id,type,value,unit". Invalid rows are logged and skipped.</summary>
public sealed class CsvSensorSource(string path, TimeSpan? replayDelay = null, TextWriter? errorLog = null)
    : ISensorSource
{
    private const string ExpectedHeader = "timestamp,sensor_id,type,value,unit";

    public string Name => $"CSV:{Path.GetFileName(path)}";

    public async IAsyncEnumerable<SensorReading> ReadAsync(
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        using var reader = new StreamReader(path);

        var headerChecked = false;
        var lineNumber = 0;
        string? line;
        while ((line = await reader.ReadLineAsync(ct)) is not null)
        {
            lineNumber++;
            var trimmed = line.Trim();
            if (trimmed.Length == 0) continue; // blank lines are skipped silently

            if (!headerChecked)
            {
                if (trimmed != ExpectedHeader)
                {
                    throw new InvalidOperationException(
                        $"Invalid CSV header in '{path}': expected \"{ExpectedHeader}\", found \"{trimmed}\".");
                }

                headerChecked = true;
                continue;
            }

            var reading = TryParse(trimmed);
            if (reading is null)
            {
                (errorLog ?? Console.Error).WriteLine($"line {lineNumber}: {line}");
                continue;
            }

            if (replayDelay is not null)
            {
                await Task.Delay(replayDelay.Value, ct);
            }

            yield return reading;
        }
    }

    private static SensorReading? TryParse(string line)
    {
        var columns = line.Split(',');
        if (columns.Length != 5) return null;

        // Invariant culture only: a comma-decimal locale must not break parsing.
        var timestampOk = DateTimeOffset.TryParse(
            columns[0], CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var timestamp);
        var valueOk = double.TryParse(
            columns[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            && !double.IsNaN(value);
        var typeText = columns[2].Trim();
        var typeOk = Enum.TryParse<SensorType>(typeText, ignoreCase: true, out var type)
            && !typeText.All(char.IsDigit) // bare digits would parse as the raw enum value
            && Enum.IsDefined(type);

        if (!timestampOk || !valueOk || !typeOk) return null;

        return new SensorReading(timestamp, columns[1].Trim(), type, value, columns[4].Trim());
    }
}
