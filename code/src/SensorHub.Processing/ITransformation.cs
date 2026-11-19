using SensorHub.Core;

namespace SensorHub.Processing;

/// <summary>One step of the transformation chain.</summary>
public interface ITransformation
{
    /// <summary>Returns the (possibly changed) reading. A step that detects a
    /// problem adds an <see cref="Alert"/> to <paramref name="alerts"/>.</summary>
    SensorReading Apply(SensorReading reading, ICollection<Alert> alerts);
}
