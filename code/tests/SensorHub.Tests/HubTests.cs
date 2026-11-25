using SensorHub.Application;
using SensorHub.Core;
using SensorHub.Ingestion;
using SensorHub.Output;
using SensorHub.Processing;

namespace SensorHub.Tests;

public class HubTests
{
    // --- Small fakes, so the Hub is tested on its own ---

    private sealed class FakeSource(params string[] ids) : ISensorSource
    {
        public IEnumerable<SensorReading> Read() =>
            ids.Select(id => new SensorReading { SensorId = id, Type = SensorType.Temperature, Value = 20, Unit = "C" });
    }

    private sealed class ThrowingSource : ISensorSource
    {
        public IEnumerable<SensorReading> Read()
        {
            yield return new SensorReading { SensorId = "ok" };
            throw new InvalidOperationException("sensor broke");
        }
    }

    private sealed class RecordingSink : IOutputSink
    {
        public List<string> Written { get; } = [];
        public bool Closed { get; private set; }
        public void Write(SensorReading reading) => Written.Add(reading.SensorId);
        public void Close() => Closed = true;
    }

    private sealed class AddOne : ITransformation
    {
        public SensorReading Apply(SensorReading reading)
        {
            reading.Value += 1;
            return reading;
        }
    }

    private static Hub MakeHub(List<ISensorSource> sources, List<IOutputSink> sinks, ITransformation? chain = null) =>
        new(sources, chain ?? new TransformationChain(), sinks, TimeSpan.Zero);

    // --- Tests ---

    [Fact]
    public void Run_ReadsSourcesRoundRobin()
    {
        var sink = new RecordingSink();

        MakeHub([new FakeSource("a1", "a2", "a3"), new FakeSource("b1"), new FakeSource("c1", "c2")], [sink]).Run();

        // One from each source in turn; a source that runs out is skipped.
        Assert.Equal(["a1", "b1", "c1", "a2", "c2", "a3"], sink.Written);
    }

    [Fact]
    public void Run_SendsEveryProcessedReadingToEverySink()
    {
        var first = new RecordingSink();
        var values = new List<double>();
        var second = new ValueSink(values);

        MakeHub([new FakeSource("a1", "a2")], [first, second], new AddOne()).Run();

        Assert.Equal(["a1", "a2"], first.Written);
        Assert.Equal([21.0, 21.0], values); // the chain ran before the sinks: 20 + 1
    }

    [Fact]
    public void Run_ClosesSinksAtTheEnd()
    {
        var sink = new RecordingSink();

        MakeHub([new FakeSource("a1")], [sink]).Run();

        Assert.True(sink.Closed);
    }

    [Fact]
    public void Run_ClosesSinksEvenWhenASourceCrashes()
    {
        var sink = new RecordingSink();

        var error = Assert.Throws<InvalidOperationException>(() => MakeHub([new ThrowingSource()], [sink]).Run());

        Assert.Equal("sensor broke", error.Message);
        Assert.Equal(["ok"], sink.Written); // what came before the crash was delivered
        Assert.True(sink.Closed);
    }

    [Fact]
    public void Run_WaitsTheDelayBetweenReadings()
    {
        var hub = new Hub([new FakeSource("a1", "a2", "a3")], new TransformationChain(), [new RecordingSink()],
            TimeSpan.FromMilliseconds(30));

        var watch = System.Diagnostics.Stopwatch.StartNew();
        hub.Run();

        Assert.True(watch.ElapsedMilliseconds >= 80, $"took only {watch.ElapsedMilliseconds} ms"); // 3 × 30 ms, with slack
    }

    [Fact]
    public void Constructor_RejectsNoSourceNoSinkOrNegativeDelay()
    {
        var chain = new TransformationChain();

        Assert.Throws<ArgumentException>(() => new Hub([], chain, [new RecordingSink()], TimeSpan.Zero));
        Assert.Throws<ArgumentException>(() => new Hub([new FakeSource("a")], chain, [], TimeSpan.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new Hub([new FakeSource("a")], chain, [new RecordingSink()], TimeSpan.FromMilliseconds(-1)));
    }

    private sealed class ValueSink(List<double> values) : IOutputSink
    {
        public void Write(SensorReading reading) => values.Add(reading.Value);
        public void Close() { }
    }
}
