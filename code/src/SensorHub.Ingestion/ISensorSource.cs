using SensorHub.Core;

namespace SensorHub.Ingestion;

/// <summary>A producer of sensor readings (CSV file, random generator, ...).</summary>
public interface ISensorSource
{
    /// <summary>Reads all available readings.</summary>
    IEnumerable<SensorReading> Read();
}
