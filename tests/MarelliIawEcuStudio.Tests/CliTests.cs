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
}
