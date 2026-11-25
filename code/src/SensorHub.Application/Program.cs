using SensorHub.Configuration;
using SensorHub.Output;

namespace SensorHub.Application;

public static class Program
{
    /// <summary>Usage: SensorHub.Application [path/to/config.json]
    /// (defaults to config.json in the current folder).</summary>
    public static void Main(string[] args)
    {
        var configPath = Path.GetFullPath(args.Length > 0 ? args[0] : "config.json");

        Hub hub;
        var sinks = new List<IOutputSink>();
        try
        {
            var config = new ConfigLoader().Load(configPath);

            // Relative paths in the config (CSV files, log file) are relative to the config file.
            Directory.SetCurrentDirectory(Path.GetDirectoryName(configPath)!);

            var sourceFactory = new SourceFactory();
            var sources = config.Sources.Select(sourceFactory.Create).ToList();
            var chain = new TransformationFactory().CreateChain(config.Transformations);
            var sinkFactory = new SinkFactory();
            foreach (var sinkConfig in config.Sinks)
                sinks.Add(sinkFactory.Create(sinkConfig));

            hub = new Hub(sources, chain, sinks, TimeSpan.FromMilliseconds(config.DelayMs));
        }
        catch (Exception e) when (e is InvalidOperationException or ArgumentException or IOException)
        {
            foreach (var sink in sinks) sink.Close(); // e.g. the log file opened before a later sink failed
            Console.Error.WriteLine($"Configuration error: {e.Message}");
            Environment.ExitCode = 1;
            return;
        }

        hub.Run();
    }
}
