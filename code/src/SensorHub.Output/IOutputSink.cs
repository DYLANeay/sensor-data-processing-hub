using SensorHub.Core;

namespace SensorHub.Output;

/// <summary>An output destination for processed readings (console, file, ...).</summary>
public interface IOutputSink
{
    /// <summary>Delivers one processed reading, together with its alerts.</summary>
    void Write(SensorReading reading);

    /// <summary>Called once when the hub stops, to flush and release resources.</summary>
    void Close();
}
