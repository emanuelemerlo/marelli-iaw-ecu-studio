using System.Globalization;
using MarelliIawEcuStudio.Core.Definitions;
using MarelliIawEcuStudio.Core.Logging;
using MarelliIawEcuStudio.Core.Protocol;
using MarelliIawEcuStudio.Core.Session;
using MarelliIawEcuStudio.Simulator;

namespace MarelliIawEcuStudio.Tests;

public class CsvLogTests
{
    [Fact]
    public void LogRoundTripsWithMetadata()
    {
        var registry = DefinitionRegistry.BuiltIn();
        var ecu = new SimulatedEcu(registry.Get("IAW-8F/18F"));
        using var session = new DiagnosticSession(new SimulatorTransport(ecu), registry, new SessionOptions { Timing = ProtocolTiming.Fast, ListenForIsoBroadcast = false });
        var c = session.Connect(null, TestContext.Current.CancellationToken);
        var columns = new[] { session.GetParameter("PERIODE"), session.GetParameter("M_VBATT") };

        var text = new StringWriter();
        using (var log = new CsvLogWriter(text, c, columns, "0.1.0"))
        {
            foreach (var (t, rpm) in new[] { (0L, 900.0), (250L, 2500.0) })
            {
                ecu.SetValue("PERIODE", rpm);
                ecu.SetValue("M_VBATT", 14);
                session.PollOnce(new PollRequest { Parameters = columns });
                log.WriteSample(t, session.Buffer);
            }
        }

        var read = CsvLogReader.Read(new StringReader(text.ToString()));
        Assert.False(read.IsLegacyIes2);
        Assert.Equal("IAW-8F/18F", read.Metadata["ecu"]);
        Assert.Equal(c.Iso, read.Metadata["iso"]);
        Assert.Equal(["PERIODE", "M_VBATT"], read.Columns.Select(x => x.Id));
        Assert.Equal("V", read.Columns[1].Unit);
        Assert.Equal([0L, 250L], read.Samples.Select(s => s.TimeMs));
        Assert.Equal(2500, read.Samples[1].Values[0], 0);
        Assert.Equal(14, read.Samples[1].Values[1], 1);
    }

    [Fact]
    public void ReadsLegacyIes2ExportsWithCommaDecimals()
    {
        const string legacy = "Timestamp;Engine Speed;Battery Voltage\r\n0;850;13,75\r\n200;860;13,8125\r\n";
        var log = CsvLogReader.Read(new StringReader(legacy));
        Assert.True(log.IsLegacyIes2);
        Assert.Equal(["Engine Speed", "Battery Voltage"], log.Columns.Select(c => c.Name));
        Assert.Equal(13.8125, log.Samples[1].Values[1]);
    }

    [Fact]
    public void InvalidValuesAreWrittenAsEmptyCells()
    {
        var registry = DefinitionRegistry.BuiltIn();
        var transport = new SimulatorTransport(new SimulatedEcu(registry.Get("IAW-8F/18F")));
        using var session = new DiagnosticSession(transport, registry, new SessionOptions { Timing = ProtocolTiming.Fast, ListenForIsoBroadcast = false });
        var c = session.Connect(null, TestContext.Current.CancellationToken);
        var text = new StringWriter(CultureInfo.InvariantCulture);
        using (var log = new CsvLogWriter(text, c, [session.GetParameter("PERIODE")], "0.1.0"))
            log.WriteSample(0, session.Buffer); // nothing polled yet
        Assert.EndsWith("0;" + Environment.NewLine, text.ToString());
        Assert.True(double.IsNaN(CsvLogReader.Read(new StringReader(text.ToString())).Samples[0].Values[0]));
    }
}
