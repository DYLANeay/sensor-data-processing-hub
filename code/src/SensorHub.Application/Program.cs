using SensorHub.Core;
using SensorHub.Ingestion;
using SensorHub.Output;
using SensorHub.Processing;

// Demo: reads the sample CSV and a simulated sensor, runs each reading
// through a transformation chain, and sends it to the console dashboard and
// the log file. Replaced by Hub + config.json in the next issues.

// sample.csv is copied to the output directory (see csproj),
// so this works regardless of the current working directory.
var csvPath = Path.Combine(AppContext.BaseDirectory, "data", "sample.csv");

var chain = new TransformationChain();
chain.Add(new UnitConverter(SensorType.Temperature, "C", "F", 1.8, 32));
chain.Add(new MovingAverageSmoother(windowSize: 3));
chain.Add(new ThresholdAlerter(SensorType.Temperature, warningThreshold: 74, criticalThreshold: 78));

var sources = new List<ISensorSource>
{
    new CsvSensorSource(csvPath),
    new RandomSensorSource("sim-01", SensorType.Temperature, "C", 18.0, 30.0, 5),
};
var sinks = new List<IOutputSink> { new ConsoleDashboardSink(), new LogFileSink("hub.log") };

foreach (var source in sources)
    foreach (var reading in source.Read())
    {
        var processed = chain.Apply(reading);
        foreach (var sink in sinks)
            sink.Write(processed);
        Thread.Sleep(300); // slow enough to watch the dashboard update
    }

foreach (var sink in sinks)
    sink.Close();

Console.WriteLine($"\nLog written to {Path.GetFullPath("hub.log")}");
