using MarelliIawEcuStudio.Cli;

namespace MarelliIawEcuStudio.Tests;

public class CliTests
{
    private static (int Code, string Output) Run(string stdin, params string[] args)
    {
        var output = new StringWriter();
        var code = new CliApp(output, new StringReader(stdin)).Run([.. args, "--no-broadcast"], TestContext.Current.CancellationToken);
        return (code, output.ToString());
    }

    [Fact]
    public void ListsEcuFamilies()
    {
        var (code, output) = Run("", "ecus");
        Assert.Equal(0, code);
        Assert.Contains("IAW-04K.P8", output);
        Assert.Contains("Alfa 145/146 1.3 Boxer", output);
    }

    [Fact]
    public void IdentifiesAndReadsFromTheSimulator()
    {
        var (code, output) = Run("", "read", "--simulate", "IAW-8F/18F", "--params", "PERIODE,M_VBATT");
        Assert.Equal(0, code);
        Assert.Contains("Magneti-Marelli IAW-8F/18F MPI", output);
        Assert.Contains("Engine Speed", output);
        Assert.Contains("Battery Voltage", output);
    }

    [Fact]
    public void WritesCsvLog()
    {
        var file = Path.Combine(Path.GetTempPath(), $"iaw-{Guid.NewGuid():N}.csv");
        try
        {
            var (code, _) = Run("", "read", "--simulate", "IAW-8F/18F", "--count", "3", "--csv", file);
            Assert.Equal(0, code);
            var log = Core.Logging.CsvLogReader.Read(file);
            Assert.Equal(3, log.Samples.Count);
            Assert.Equal(24, log.Columns.Count);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public void WritesAndReplaysALineTrace()
    {
        var file = Path.Combine(Path.GetTempPath(), $"iaw-{Guid.NewGuid():N}.trace.txt");
        try
        {
            var (code, recorded) = Run("", "read", "--simulate", "IAW-8F/18F", "--params", "PERIODE,M_VBATT", "--count", "3", "--trace", file);
            Assert.Equal(0, code);
            Assert.Contains("# line: ", File.ReadAllText(file));

            var (summaryCode, summary) = Run("", "trace", file);
            Assert.Equal(0, summaryCode);
            Assert.Contains("simulator:IAW-8F/18F", summary);
            Assert.Contains("0 without answer", summary);
            Assert.Contains("identified ecu=IAW-8F/18F", summary);

            // Same command against the trace: same identification and values.
            var (replayCode, replayed) = Run("", "read", "--replay", file, "--params", "PERIODE,M_VBATT", "--count", "3");
            Assert.Equal(0, replayCode);
            static IEnumerable<string> Values(string o) => o.Split('\n').Where(l => l.StartsWith("Engine Speed", StringComparison.Ordinal) || l.StartsWith("Battery", StringComparison.Ordinal) || l.StartsWith("ISO:", StringComparison.Ordinal));
            Assert.Equal(Values(recorded), Values(replayed));

            // Reading on stops cleanly at the end of the trace.
            var (endCode, end) = Run("", "read", "--replay", file, "--params", "PERIODE,M_VBATT", "--count", "0");
            Assert.Equal(0, endCode);
            Assert.Contains("End of the trace after 3 cycles.", end);

            // Other requests: strict mode stops, lookup mode answers.
            var (divergedCode, diverged) = Run("", "read", "--replay", file, "--params", "MT_EAU_L");
            Assert.Equal(1, divergedCode);
            Assert.Contains("diverged", diverged);
            var (lookupCode, lookup) = Run("", "read", "--replay", file, "--replay-mode", "lookup", "--params", "M_VBATT", "--count", "5");
            Assert.Equal(0, lookupCode);
            Assert.Contains("Battery Voltage", lookup);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public void ActiveCommandsNeedExplicitPermission()
    {
        var (code, output) = Run("", "clear-codes", "--simulate");
        Assert.Equal(1, code);
        Assert.Contains("--allow-active", output);
    }

    [Fact]
    public void ActiveCommandsAskForConfirmation()
    {
        var (declined, _) = Run("n\n", "test", "1", "--simulate", "--allow-active");
        Assert.Equal(1, declined);
        var (accepted, output) = Run("y\n", "test", "1", "--simulate", "--allow-active");
        Assert.Equal(0, accepted);
        Assert.Contains("Passed", output);
    }

    [Fact]
    public void ReportsErrors()
    {
        var (code, output) = Run("", "errors", "--simulate", "IAW-04K.P8", "--ecu", "IAW-04K.P8");
        Assert.Equal(0, code);
        Assert.Contains("No errors.", output);
    }

    [Fact]
    public void ValidatesDefinitionFiles()
    {
        var dir = Directory.CreateTempSubdirectory("iaw-validate-");
        try
        {
            File.WriteAllText(Path.Combine(dir.FullName, "bad.json"), "{\"id\": \"X\", \"name\": \"X\", \"protocol\": {\"identification\": \"kwp\"}}");
            var (code, output) = Run("", "validate", dir.FullName);
            Assert.Equal(1, code);
            Assert.Contains("INVALID", output);
            Assert.Contains("kwp", output);
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Fact]
    public void UnknownCommandIsAnError()
    {
        var (code, output) = Run("", "frobnicate");
        Assert.Equal(2, code);
        Assert.Contains("Unknown command", output);
    }

    [Fact]
    public void SerialPortIsRequiredWithoutSimulator()
    {
        var (code, output) = Run("", "identify");
        Assert.Equal(2, code);
        Assert.Contains("--port", output);
    }

    [Fact]
    public void AcceptsTheLabelCodeOfTheEcu()
    {
        var (code, output) = Run("", "identify", "--simulate", "4WF.P8", "--ecu", "4WF.P8");
        Assert.Equal(0, code);
        Assert.Contains("Weber-Marelli IAW-04K.P8 MPI", output);
        var (_, list) = Run("", "ecus");
        Assert.Contains("also known as IAW 4WF.P8", list);
    }
}
