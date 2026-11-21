using SensorHub.Core;
using SensorHub.Ingestion;
using SensorHub.Processing;

// Demo: reads the sample CSV and a simulated sensor, runs each reading
// through a transformation chain, and prints the result plus any alerts.

// sample.csv is copied to the output directory (see csproj),
// so this works regardless of the current working directory.
var csvPath = Path.Combine(AppContext.BaseDirectory, "data", "sample.csv");

var chain = new TransformationChain();
chain.Add(new UnitConverter(SensorType.Temperature, "C", "F", 1.8, 32));
chain.Add(new MovingAverageSmoother(windowSize: 3));
chain.Add(new ThresholdAlerter(SensorType.Temperature, warningThreshold: 74, criticalThreshold: 78));

var csvSource = new CsvSensorSource(csvPath);
Console.WriteLine($"--- {Path.GetFileName(csvPath)} ---");
foreach (var reading in csvSource.Read())
    Print(chain.Apply(reading));

var sensor = new RandomSensorSource("sim-01", SensorType.Temperature, "C", 18.0, 24.0, 5);
Console.WriteLine("--- sim-01 ---");
foreach (var reading in sensor.Read())
    Print(chain.Apply(reading));

static void Print(SensorReading reading)
{
    Console.WriteLine(
        $"{reading.Timestamp:O} {reading.SensorId} {reading.Type} = {reading.Value:0.##} {reading.Unit}");
    foreach (var alert in reading.Alerts)
        Console.WriteLine($"  [{alert.Severity}] {alert.Message}");
}
