using SensorHub.Configuration;
using SensorHub.Core;
using SensorHub.Ingestion;
using SensorHub.Output;
using SensorHub.Processing;

namespace SensorHub.Tests;

public class ConfigurationTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("sensorhub-config-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private string WriteConfig(string json)
    {
        var path = Path.Combine(_dir, "config.json");
        File.WriteAllText(path, json);
        return path;
    }

    private static ComponentConfig Entry(string type, params (string Key, string Value)[] settings) =>
        new() { Type = type, Settings = settings.ToDictionary(s => s.Key, s => s.Value) };

    private static SensorReading Reading(double value, SensorType type = SensorType.Temperature, string unit = "C") =>
        new() { SensorId = "room-a", Type = type, Value = value, Unit = unit };

    // --- ConfigLoader ---

    [Fact]
    public void Loader_ReadsSourcesTransformationsAndSinks()
    {
        var config = new ConfigLoader().Load(WriteConfig("""
            {
              // comments and trailing commas are allowed
              "Sources": [ { "type": "csv", "settings": { "path": "data/room-a.csv" } } ],
              "transformations": [ { "type": "movingAverage", "settings": { "windowSize": "3" } } ],
              "sinks": [ { "type": "console" }, ],
            }
            """));

        Assert.Equal("csv", config.Sources.Single().Type);
        Assert.Equal("data/room-a.csv", config.Sources.Single().Settings["path"]);
        Assert.Equal("3", config.Transformations.Single().Settings["windowSize"]);
        Assert.Equal("console", config.Sinks.Single().Type);
    }

    [Fact]
    public void Loader_TransformationsAreOptional()
    {
        var config = new ConfigLoader().Load(WriteConfig("""
            { "sources": [ { "type": "csv" } ], "sinks": [ { "type": "console" } ] }
            """));

        Assert.Empty(config.Transformations);
        Assert.Equal(0, config.DelayMs); // no delay unless asked for
    }

    [Fact]
    public void Loader_ReadsDelayAndRejectsNegative()
    {
        var loader = new ConfigLoader();

        Assert.Equal(200, loader.Load(WriteConfig("""
            { "delayMs": 200, "sources": [ { "type": "csv" } ], "sinks": [ { "type": "console" } ] }
            """)).DelayMs);
        Assert.Contains("delayMs must not be negative", Assert.Throws<InvalidOperationException>(() => loader.Load(WriteConfig("""
            { "delayMs": -5, "sources": [ { "type": "csv" } ], "sinks": [ { "type": "console" } ] }
            """))).Message);
    }

    [Theory]
    [InlineData("""{ "sinks": [ { "type": "console" } ] }""", "at least one source")]
    [InlineData("""{ "sources": [ { "type": "csv" } ], "sinks": [] }""", "at least one sink")]
    [InlineData("""{ "sources": [ { "type": "csv", "settings": { "count": 5 } } ], "sinks": [] }""", "$.sources[0].settings.count")]
    [InlineData("null", "empty")]
    public void Loader_RejectsUnusableConfig(string json, string expectedInMessage)
    {
        var error = Assert.Throws<InvalidOperationException>(() => new ConfigLoader().Load(WriteConfig(json)));

        Assert.Contains(expectedInMessage, error.Message);
    }

    [Fact]
    public void Loader_MissingFileThrows()
    {
        Assert.Throws<FileNotFoundException>(() => new ConfigLoader().Load(Path.Combine(_dir, "nope.json")));
    }

    // --- SourceFactory ---

    [Fact]
    public void SourceFactory_CreatesCsvAndRandomSources()
    {
        var factory = new SourceFactory();

        Assert.IsType<CsvSensorSource>(factory.Create(Entry("csv", ("path", "data/room-a.csv"))));

        var random = factory.Create(Entry("Random", // type names ignore case
            ("sensorId", "sim-01"), ("sensorType", "co2"), ("unit", "ppm"),
            ("minValue", "400"), ("maxValue", "800"), ("count", "4")));
        var readings = random.Read().ToList();
        Assert.Equal(4, readings.Count);
        Assert.All(readings, r => Assert.Equal(SensorType.CO2, r.Type));
    }

    [Theory]
    [InlineData("sensorType", "Pressure", "must be one of Temperature, CO2, Humidity")]
    [InlineData("sensorType", "1", "must be one of")] // a bare number must not sneak in as an enum value
    [InlineData("minValue", "abc", "'minValue' must be a number")]
    [InlineData("count", "2.5", "'count' must be a whole number")]
    public void SourceFactory_RejectsBadSettings(string key, string badValue, string expectedInMessage)
    {
        var settings = new Dictionary<string, string>
        {
            ["sensorId"] = "sim-01", ["sensorType"] = "Temperature", ["unit"] = "C",
            ["minValue"] = "18", ["maxValue"] = "30", ["count"] = "5",
            [key] = badValue,
        };

        var error = Assert.Throws<InvalidOperationException>(() =>
            new SourceFactory().Create(new ComponentConfig { Type = "random", Settings = settings }));

        Assert.Contains(expectedInMessage, error.Message);
    }

    [Fact]
    public void SourceFactory_ReportsMissingSettingAndUnknownType()
    {
        var factory = new SourceFactory();

        Assert.Contains("missing setting 'path'",
            Assert.Throws<InvalidOperationException>(() => factory.Create(Entry("csv"))).Message);
        Assert.Contains("Known types: csv, random",
            Assert.Throws<InvalidOperationException>(() => factory.Create(Entry("socket"))).Message);
    }

    // --- TransformationFactory ---

    [Fact]
    public void TransformationFactory_BuildsChainInConfigOrder()
    {
        // Convert first, then alert in Fahrenheit: 30 C = 86 F, above the 80.6 F warning.
        var chain = new TransformationFactory().CreateChain(
        [
            Entry("unitConverter", ("sensorType", "Temperature"), ("fromUnit", "C"), ("toUnit", "F"),
                ("factor", "1.8"), ("offset", "32")),
            Entry("threshold", ("sensorType", "Temperature"), ("warningThreshold", "80.6"), ("criticalThreshold", "90")),
        ]);

        var result = chain.Apply(Reading(30));

        Assert.Equal(86, result.Value, precision: 6);
        Assert.Equal("F", result.Unit);
        Assert.Equal(AlertSeverity.Warning, result.Alerts.Single().Severity);
    }

    [Fact]
    public void TransformationFactory_OffsetDefaultsToZero()
    {
        var converter = new TransformationFactory().Create(Entry("unitConverter",
            ("sensorType", "CO2"), ("fromUnit", "ppm"), ("toUnit", "%"), ("factor", "0.0001")));

        Assert.Equal(0.05, converter.Apply(Reading(500, SensorType.CO2, "ppm")).Value, precision: 9);
    }

    [Fact]
    public void TransformationFactory_CreatesSmoother()
    {
        Assert.IsType<MovingAverageSmoother>(new TransformationFactory().Create(Entry("movingAverage", ("windowSize", "3"))));
    }

    [Fact]
    public void TransformationFactory_ConstructorChecksStillApply()
    {
        // warning above critical is caught by ThresholdAlerter itself
        Assert.Throws<ArgumentException>(() => new TransformationFactory().Create(Entry("threshold",
            ("sensorType", "CO2"), ("warningThreshold", "1200"), ("criticalThreshold", "1000"))));
    }

    // --- SinkFactory ---

    [Fact]
    public void SinkFactory_CreatesConsoleAndLogFileSinks()
    {
        var factory = new SinkFactory();
        var logPath = Path.Combine(_dir, "hub.log");

        Assert.IsType<ConsoleDashboardSink>(factory.Create(Entry("console")));
        var log = factory.Create(Entry("logFile", ("path", logPath)));
        Assert.IsType<LogFileSink>(log);
        log.Close();
        Assert.True(File.Exists(logPath));
    }

    [Fact]
    public void SinkFactory_UnknownTypeThrows()
    {
        Assert.Throws<InvalidOperationException>(() => new SinkFactory().Create(Entry("database")));
    }

    // --- The config.json shipped with the project ---

    [Fact]
    public void ShippedConfig_LoadsAndEveryEntryBuilds()
    {
        var configPath = FindShippedConfig();
        var config = new ConfigLoader().Load(configPath);

        var sources = config.Sources.Select(new SourceFactory().Create).ToList();
        new TransformationFactory().CreateChain(config.Transformations);
        Assert.True(config.Sinks.Count >= 2);
        Assert.Contains(config.Sinks, s => s.Type.Equals("console", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(config.Sinks, s => s.Type.Equals("logFile", StringComparison.OrdinalIgnoreCase));

        // Every CSV the config points to must exist next to it.
        foreach (var csv in config.Sources.Where(s => s.Type == "csv"))
            Assert.True(File.Exists(Path.Combine(Path.GetDirectoryName(configPath)!, csv.Settings["path"])), csv.Settings["path"]);
        Assert.Equal(2, sources.Count);
    }

    // Walks up from the test binaries (code/tests/.../bin/Debug/net8.0) to code/config.json.
    private static string FindShippedConfig()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "config.json");
            if (File.Exists(candidate) && File.Exists(Path.Combine(dir.FullName, "SensorHub.sln")))
                return candidate;
        }
        throw new FileNotFoundException("code/config.json not found above the test output folder.");
    }
}
