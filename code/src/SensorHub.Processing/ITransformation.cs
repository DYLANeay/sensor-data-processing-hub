using SensorHub.Core;

namespace SensorHub.Processing;

/// <summary>One step of the transformation chain. A step that detects a
/// problem adds an <see cref="Alert"/> to <c>reading.Alerts</c>.</summary>
public interface ITransformation
{
    SensorReading Apply(SensorReading reading);
}
