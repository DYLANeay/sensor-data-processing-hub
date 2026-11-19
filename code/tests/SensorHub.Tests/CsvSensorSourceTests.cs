using System.Globalization;
using SensorHub.Core;
using SensorHub.Ingestion;

namespace SensorHub.Tests;

public class CsvSensorSourceTests : IDisposable
{
    private readonly List<string> _tempFiles = [];

    public void Dispose()
    {
        foreach (var path in _tempFiles) File.Delete(path);
    }

    [Fact]
    public async Task ValidRows_AreParsedWithExpectedValues()
    {
        var (readings, _) = await ReadCsv("""
            timestamp,sensor_id,type,value,unit
            2026-11-19T08:00:00Z,room-101,temperature,21.3,C
            2026-11-19T08:00:10Z,room-101,co2,610,ppm
            2026-11-19T08:00:20Z,room-101,humidity,44.2,%
            """);

        Assert.Equal(3, readings.Count);
        Assert.Equal(new DateTimeOffset(2026, 11, 19, 8, 0, 0, TimeSpan.Zero), readings[0].Timestamp);
        Assert.Equal("room-101", readings[0].SensorId);
        Assert.Equal(SensorType.Temperature, readings[0].Type);
        Assert.Equal(21.3, readings[0].Value, precision: 3);
        Assert.Equal("C", readings[0].Unit);
        Assert.Equal(610, readings[1].Value, precision: 3);
        Assert.Equal(44.2, readings[2].Value, precision: 3);
    }

    [Fact]
    public async Task InvalidRow_IsSkippedAndReported()
    {
        var (readings, errors) = await ReadCsv("""
            timestamp,sensor_id,type,value,unit
            2026-11-19T08:00:00Z,room-101,temperature,21.3,C
            2026-11-19T08:00:10Z,room-101,temperature,,C
            2026-11-19T08:00:20Z,room-101,temperature,21.5,C
            """);

        Assert.Equal(2, readings.Count);
        Assert.Equal(21.3, readings[0].Value, precision: 3);
        Assert.Equal(21.5, readings[1].Value, precision: 3);
        Assert.Contains("line 3", errors);
        Assert.Contains("temperature,,C", errors);
    }

    [Fact]
    public async Task UnknownType_IsSkippedAndReported()
    {
        var (readings, errors) = await ReadCsv("""
            timestamp,sensor_id,type,value,unit
            2026-11-19T08:00:00Z,room-101,pressure,1013.2,hPa
            2026-11-19T08:00:10Z,room-101,co2,610,ppm
            """);

        Assert.Single(readings);
        Assert.Equal(SensorType.CO2, readings[0].Type);
        Assert.Contains("line 2", errors);
        Assert.Contains("pressure", errors);
    }

    [Fact]
    public async Task Value_ParsesInvariantlyUnderCommaDecimalCulture()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("nb-NO");
        try
        {
            var (readings, _) = await ReadCsv("""
                timestamp,sensor_id,type,value,unit
                2026-11-19T08:00:00Z,room-101,temperature,21.4,C
                """);

            Assert.Single(readings);
            Assert.Equal(21.4, readings[0].Value, precision: 3);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    // Writes csv to a temp file, reads it back, and returns the parsed
    // readings plus everything written to the error log.
    private async Task<(List<SensorReading> Readings, string Errors)> ReadCsv(string csv)
    {
        var path = Path.GetTempFileName();
        _tempFiles.Add(path);
        File.WriteAllText(path, csv);

        var errors = new StringWriter();
        var readings = new List<SensorReading>();
        await foreach (var reading in new CsvSensorSource(path, errorLog: errors).ReadAsync())
            readings.Add(reading);
        return (readings, errors.ToString());
    }
}
