using SensorHub.Configuration;
using SensorHub.Ingestion;
using SensorHub.Output;
using SensorHub.Processing;

namespace SensorHub.Application;

public static class Program
{
    /// <summary>Usage: SensorHub.Application [path/to/config.json]
    /// (defaults to config.json in the current folder).</summary>
    public static void Main(string[] args)
    {
        var configPath = Path.GetFullPath(args.Length > 0 ? args[0] : "config.json");

        List<ISensorSource> sources;
        TransformationChain chain;
        var sinks = new List<IOutputSink>();
        try
        {
            var config = new ConfigLoader().Load(configPath);

            // Relative paths in the config (CSV files, log file) are relative to the config file.
            Directory.SetCurrentDirectory(Path.GetDirectoryName(configPath)!);

            var sourceFactory = new SourceFactory();
            sources = config.Sources.Select(sourceFactory.Create).ToList();
            chain = new TransformationFactory().CreateChain(config.Transformations);
            var sinkFactory = new SinkFactory();
            foreach (var sinkConfig in config.Sinks)
                sinks.Add(sinkFactory.Create(sinkConfig));
        }
        catch (Exception e) when (e is InvalidOperationException or ArgumentException or IOException)
        {
            foreach (var sink in sinks) sink.Close(); // e.g. the log file opened before a later sink failed
            Console.Error.WriteLine($"Configuration error: {e.Message}");
            Environment.ExitCode = 1;
            return;
        }

        // shortcut: the processing loop lives here until Hub exists (next issue), then becomes hub.Run().
        try
        {
            foreach (var source in sources)
                foreach (var reading in source.Read())
                {
                    var processed = chain.Apply(reading);
                    foreach (var sink in sinks)
                        sink.Write(processed);
                }
        }
        finally
        {
            foreach (var sink in sinks)
                sink.Close();
        }
    }
}
