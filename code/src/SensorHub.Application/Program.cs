using SensorHub.Core;
using SensorHub.Ingestion;

// Demo: prints the sample CSV, then 5 simulated readings.

// sample.csv is copied to the output directory (see csproj),
// so this works regardless of the current working directory.
var csvPath = Path.Combine(AppContext.BaseDirectory, "data", "sample.csv");

var csvSource = new CsvSensorSource(csvPath);
Console.WriteLine($"--- {csvSource.Name} ---");
await foreach (var reading in csvSource.ReadAsync())
    Print(reading);

var sensor = new RandomSensorSource(
    sensorId: "sim-01", type: SensorType.Temperature, unit: "C",
    baseline: 21.0, amplitude: 1.5, noiseStdDev: 0.2,
    interval: TimeSpan.FromMilliseconds(10), seed: 42, count: 5);
Console.WriteLine($"--- {sensor.Name} ---");
await foreach (var reading in sensor.ReadAsync())
    Print(reading);

static void Print(SensorReading reading) =>
    Console.WriteLine(
        $"{reading.Timestamp:O} {reading.SensorId} {reading.Type} = {reading.Value:0.##} {reading.Unit}");
