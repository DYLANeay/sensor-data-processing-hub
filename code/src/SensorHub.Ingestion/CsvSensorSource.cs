using System.Globalization;
using SensorHub.Core;

namespace SensorHub.Ingestion;

/// <summary>Reads readings from a CSV file with the fixed header
/// "timestamp,sensor_id,type,value,unit". Invalid rows are reported on the
/// console error output and skipped.</summary>
public class CsvSensorSource : ISensorSource
{
    private const string ExpectedHeader = "timestamp,sensor_id,type,value,unit";

    private readonly string _filePath;

    public CsvSensorSource(string filePath)
    {
        _filePath = filePath;
    }

    public IEnumerable<SensorReading> Read()
    {
        var headerChecked = false;
        var lineNumber = 0;
        foreach (var line in File.ReadLines(_filePath))
        {
            lineNumber++;
            var trimmed = line.Trim();
            if (trimmed.Length == 0) continue; // blank lines are skipped silently

            if (!headerChecked)
            {
                if (trimmed != ExpectedHeader)
                {
                    throw new InvalidOperationException(
                        $"Invalid CSV header in '{_filePath}': expected \"{ExpectedHeader}\", found \"{trimmed}\".");
                }

                headerChecked = true;
                continue;
            }

            var reading = TryParse(trimmed);
            if (reading is null)
            {
                Console.Error.WriteLine($"line {lineNumber}: {line}");
                continue;
            }

            yield return reading;
        }
    }

    private static SensorReading? TryParse(string line)
    {
        var columns = line.Split(',');
        if (columns.Length != 5) return null;

        // Invariant culture only: a comma-decimal locale must not break parsing.
        var timestampOk = DateTime.TryParse(
            columns[0], CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var timestamp);
        var valueOk = double.TryParse(
            columns[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            && !double.IsNaN(value);
        var typeText = columns[2].Trim();
        var typeOk = Enum.TryParse<SensorType>(typeText, ignoreCase: true, out var type)
            && !typeText.All(char.IsDigit) // bare digits would parse as the raw enum value
            && Enum.IsDefined(type);

        if (!timestampOk || !valueOk || !typeOk) return null;

        return new SensorReading
        {
            Timestamp = timestamp,
            SensorId = columns[1].Trim(),
            Type = type,
            Value = value,
            Unit = columns[4].Trim(),
        };
    }
}
