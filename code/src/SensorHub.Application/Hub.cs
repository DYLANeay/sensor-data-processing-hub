using SensorHub.Ingestion;
using SensorHub.Output;
using SensorHub.Processing;

namespace SensorHub.Application;

/// <summary>Runs the processing loop. Only knows the interfaces: it never
/// names a concrete source, transformation or sink.
/// Sources are read round-robin: one reading from each source in turn, so
/// every source shows up on the dashboard from the start. A source that runs
/// out is dropped; the loop ends when all are exhausted.</summary>
public sealed class Hub
{
    private readonly List<ISensorSource> _sources;
    private readonly ITransformation _chain;
    private readonly List<IOutputSink> _sinks;
    private readonly TimeSpan _delay;

    public Hub(List<ISensorSource> sources, ITransformation chain, List<IOutputSink> sinks, TimeSpan delay)
    {
        if (sources.Count == 0) throw new ArgumentException("The hub needs at least one source.", nameof(sources));
        if (sinks.Count == 0) throw new ArgumentException("The hub needs at least one sink.", nameof(sinks));
        if (delay < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(delay), "The delay must not be negative.");

        _sources = sources;
        _chain = chain;
        _sinks = sinks;
        _delay = delay;
    }

    public void Run()
    {
        // One open reader per source; each MoveNext() pulls that source's next reading.
        var readers = _sources.Select(source => source.Read().GetEnumerator()).ToList();
        try
        {
            while (readers.Count > 0)
            {
                for (var i = 0; i < readers.Count;)
                {
                    if (!readers[i].MoveNext())
                    {
                        readers[i].Dispose(); // closes e.g. the CSV file
                        readers.RemoveAt(i);  // no i++: the next reader moved into slot i
                        continue;
                    }

                    var processed = _chain.Apply(readers[i].Current);
                    foreach (var sink in _sinks)
                        sink.Write(processed);

                    if (_delay > TimeSpan.Zero)
                        Thread.Sleep(_delay); // lets the dashboard visibly update during the demo
                    i++;
                }
            }
        }
        finally
        {
            // Even after a crash: release the sources and flush/close the log file.
            foreach (var reader in readers) reader.Dispose();
            foreach (var sink in _sinks) sink.Close();
        }
    }
}
