using SensorHub.Core;

namespace SensorHub.Processing;

/// <summary>Runs the configured transformations in order on each reading.</summary>
public sealed class TransformationChain : ITransformation
{
    private readonly List<ITransformation> _steps = [];

    /// <summary>Adds a step at the end of the chain.</summary>
    public void Add(ITransformation step) => _steps.Add(step);

    public SensorReading Apply(SensorReading reading)
    {
        foreach (var step in _steps)
            reading = step.Apply(reading);
        return reading;
    }
}
