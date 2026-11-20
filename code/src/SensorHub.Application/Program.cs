using SensorHub.Core;
using SensorHub.Ingestion;

// Demo: prints the sample CSV, then 5 simulated readings.
// Replaced by the Hub once Output and Configuration exist.

// sample.csv is copied to the output directory (see csproj),
// so this works regardless of the current working directory.
var csvPath = Path.Combine(AppContext.BaseDirectory, "data", "sample.csv");

Console.WriteLine("--- CSV: sample.csv ---");
foreach (var reading in new CsvSensorSource(csvPath).Read())
    Print(reading);

Console.WriteLine("--- random: sim-01 ---");
foreach (var reading in new RandomSensorSource("sim-01", SensorType.Temperature, "C", 19.0, 23.0, 5).Read())
    Print(reading);

static void Print(SensorReading reading) =>
    Console.WriteLine(
        $"{reading.Timestamp:O} {reading.SensorId} {reading.Type} = {reading.Value:0.##} {reading.Unit}");
