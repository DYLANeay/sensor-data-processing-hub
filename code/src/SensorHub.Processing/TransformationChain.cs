using SensorHub.Core;

namespace SensorHub.Processing;

/// <summary>Runs the configured transformations in order on each reading.</summary>
public sealed class TransformationChain(IEnumerable<ITransformation> steps)
{
    private readonly IReadOnlyList<ITransformation> _steps = steps.ToList();

    public (SensorReading Reading, IReadOnlyList<Alert> Alerts) Process(SensorReading reading)
    {
        var alerts = new List<Alert>();
        foreach (var step in _steps)
            reading = step.Apply(reading, alerts);
        return (reading, alerts);
    }
}
