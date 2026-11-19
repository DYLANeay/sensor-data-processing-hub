using SensorHub.Core;

namespace SensorHub.Ingestion;

/// <summary>A producer of sensor readings (CSV file, random generator, ...).</summary>
public interface ISensorSource
{
    /// <summary>Human-readable name of this source.</summary>
    string Name { get; }

    /// <summary>Stream readings asynchronously until exhausted or cancelled.</summary>
    IAsyncEnumerable<SensorReading> ReadAsync(CancellationToken ct = default);
}
