using SensorHub.Core;

namespace SensorHub.Processing;

/// <summary>Runs its steps in order (Composite pattern): the chain is itself
/// an <see cref="ITransformation"/>, so it can be used wherever one step is.</summary>
public class TransformationChain : ITransformation
{
    private readonly List<ITransformation> _steps = [];

    public void Add(ITransformation step) => _steps.Add(step);

    public SensorReading Apply(SensorReading reading)
    {
        foreach (var step in _steps)
            reading = step.Apply(reading);
        return reading;
    }
}
