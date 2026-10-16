using System.Globalization;
using MarelliIawEcuStudio.Core.Definitions;
using MarelliIawEcuStudio.Core.Localization;

namespace MarelliIawEcuStudio.Tests;

public class DefinitionTests
{
    private static string BuiltInJson(string file)
    {
        using var s = typeof(DefinitionLoader).Assembly.GetManifestResourceStream("MarelliIawEcuStudio.Core.Definitions." + file)!;
        return new StreamReader(s).ReadToEnd();
    }

    [Fact]
    public void BuiltInDefinitionsLoadAndValidate()
    {
        var all = DefinitionLoader.LoadBuiltIn();
        Assert.Equal(6, all.Count);
        Assert.All(all, d => Assert.Empty(DefinitionLoader.Validate(d)));
        Assert.All(all, d => Assert.True(d.Verified));
    }

    [Fact]
    public void AutoDetectOrderFollowsIes2()
    {
        var order = DefinitionRegistry.BuiltIn().AutoDetectOrder.Select(d => d.Id);
        Assert.Equal(["IAW-18FD/1G7", "IAW-8F/18F", "IAW-8F.68", "IAW-6F/16F", "IAW-04K.P8"], order);
    }

    [Fact]
    public void EveryLocalisationKeyExists()
    {
        var keys = DefinitionLoader.LoadBuiltIn().SelectMany(d =>
                d.Parameters.SelectMany(p => new[] { p.NameKey, p.UnitKey, p.FormatKey })
                .Concat(d.Errors.Engine.Items.Concat(d.Errors.Immobilizer.Items).SelectMany(e => new[] { e.NameKey, e.HighReasonKey, e.LowReasonKey }))
                .Concat(d.ActiveTests.Select(t => t.NameKey))
                .Concat(d.Adjustments.Select(a => a.NameKey)))
            .OfType<string>().Concat(["Act", "Ver", "Sto", "UnknownISO"]).Distinct();
        Assert.All(keys, k => Assert.True(Localizer.TryGet(k, out _, CultureInfo.InvariantCulture), $"missing string '{k}'"));
    }

    [Fact]
    public void TranslationsAreAvailable()
    {
        Assert.Equal("Engine Speed", Localizer.Get("PERIODE", CultureInfo.InvariantCulture));
        Assert.NotEqual("Engine Speed", Localizer.Get("PERIODE", CultureInfo.GetCultureInfo("it")));
        Assert.NotEqual("Engine Speed", Localizer.Get("PERIODE", CultureInfo.GetCultureInfo("pl")));
        Assert.Equal("NoSuchKey", Localizer.Get("NoSuchKey"));
    }

    [Fact]
    public void BytesAcceptHexStringsAndNumbers()
    {
        var json = BuiltInJson("IAW-6F-16F.json").Replace("\"0x0A\"", "10");
        var d = DefinitionLoader.Parse(json, "test");
        Assert.Equal([10], d.Parameters.Single(p => p.Id == "M_VBATT").Requests);
    }

    [Theory]
    [InlineData("\"expr\": \"x * 0.0625\"", "\"expr\": \"x *\"", "M_VBATT")]
    [InlineData("\"raw\": \"u16\"", "\"raw\": \"u32\"", "unknown raw type")]
    [InlineData("\"identification\": \"standard\"", "\"identification\": \"kwp2000\"", "identification profile")]
    [InlineData("\"scheme\": \"storedFirst\"", "\"scheme\": \"other\"", "unknown scheme")]
    [InlineData("\"type\": \"toggle\"", "\"type\": \"slider\"", "unknown type")]
    public void InvalidDefinitionsAreRejectedWithAClearMessage(string find, string replace, string expectedMessage)
    {
        var json = BuiltInJson("IAW-6F-16F.json");
        Assert.Contains(find, json);
        var ex = Assert.Throws<DefinitionException>(() => DefinitionLoader.Parse(ReplaceFirst(json, find, replace), "broken.json"));
        Assert.Contains(expectedMessage, ex.Message);
        Assert.StartsWith("broken.json", ex.Issues[0].Origin);
    }

    [Fact]
    public void MissingRequiredFieldIsReported()
    {
        var ex = Assert.Throws<DefinitionException>(() => DefinitionLoader.Parse("{\"name\": \"x\"}", "x.json"));
        Assert.Contains("id", ex.Message);
    }

    [Fact]
    public void UserFolderOverridesBuiltInAndSkipsBrokenFiles()
    {
        var dir = Directory.CreateTempSubdirectory("iaw-defs-");
        try
        {
            var json = BuiltInJson("IAW-8F68.json").Replace("Magneti-Marelli IAW-8F.68 MPI (Alfa 33)", "Corrected name");
            File.WriteAllText(Path.Combine(dir.FullName, "override.json"), json);
            File.WriteAllText(Path.Combine(dir.FullName, "broken.json"), "{ not json");
            var registry = DefinitionRegistry.Load(dir.FullName);
            Assert.Equal("Corrected name", registry.Get("IAW-8F.68").LongName);
            Assert.Equal(6, registry.All.Count);
            var issue = Assert.Single(registry.Issues);
            Assert.EndsWith("broken.json", issue.Origin);
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Fact]
    public void CodeMatchChecksOffsetAndLength()
    {
        var m = new CodeMatch(2, "D085");
        Assert.True(m.IsMatch("55D085029440"));
        Assert.False(m.IsMatch("D08555555555"));
        Assert.False(m.IsMatch("55D0"));
        Assert.False(m.IsMatch(null));
    }

    private static string ReplaceFirst(string s, string find, string replace)
    {
        var i = s.IndexOf(find, StringComparison.Ordinal);
        return s[..i] + replace + s[(i + find.Length)..];
    }
}

public class DisplayAndFamilyTests
{
    private static readonly DefinitionRegistry Registry = DefinitionRegistry.BuiltIn();

    [Fact]
    public void EveryParameterHasADashboardPlace()
    {
        foreach (var d in Registry.All)
            foreach (var p in d.Parameters)
            {
                Assert.NotNull(p.Display);
                if (p.Decode.Raw == "bit")
                    Assert.Equal(DisplaySpec.WidgetLed, p.Display.Widget);
            }
    }

    [Fact]
    public void GaugeScalesCoverTheUsualValues()
    {
        var rpm = Registry.Get("IAW-8F/18F").Parameters.Single(p => p.Id == "PERIODE").Display!;
        Assert.Equal(DisplaySpec.WidgetGauge, rpm.Widget);
        Assert.Equal(7000, rpm.Max);
        var turbo = Registry.Get("IAW-04K.P8").Parameters.Single(p => p.Id == "MP2_MP8t").Display!;
        Assert.Equal(DisplaySpec.GroupEngine, turbo.Group);
        Assert.Contains(turbo.Zones, z => z.Level == "danger");
        var stepper04k = Registry.Get("IAW-04K.P8").Parameters.Single(p => p.Id == "ALFAR").Display!;
        Assert.Equal(-128, stepper04k.Min); // IAW-04K reports the idle stepper as x - 128
    }

    [Fact]
    public void KnockFlagIsAWarningLed()
    {
        var knock = Registry.Get("IAW-8F/18F").Parameters.Single(p => p.NameKey == "StaAdvDecr").Display!;
        Assert.Equal("warning", knock.Led);
    }

    [Fact]
    public void EveryFamilyDescribesItsVehiclesAsIndicative()
    {
        foreach (var d in Registry.All)
        {
            Assert.NotNull(d.Family);
            Assert.False(d.Family.CompatibilityVerified);
            Assert.NotEmpty(d.Family.Compatibility);
            Assert.False(string.IsNullOrEmpty(d.Family.Period));
            Assert.NotEqual(d.Family.Technology!.Get(System.Globalization.CultureInfo.GetCultureInfo("it")),
                d.Family.Technology.Get(System.Globalization.CultureInfo.InvariantCulture));
        }
        Assert.Contains(Registry.Get("IAW-18FD/1G7").Family!.Compatibility, c => c.Model.Contains("Palio"));
    }

    [Fact]
    public void LocalizedTextFallsBackToEnglish()
    {
        var text = new LocalizedText { ["en"] = "Single point", ["it"] = "Monopunto" };
        Assert.Equal("Monopunto", text.Get(System.Globalization.CultureInfo.GetCultureInfo("it-IT")));
        Assert.Equal("Single point", text.Get(System.Globalization.CultureInfo.GetCultureInfo("de")));
    }

    [Theory]
    [InlineData("\"widget\": \"gauge\"", "\"widget\": \"dial\"", "unknown display widget")]
    [InlineData("\"group\": \"engine\"", "\"group\": \"cabin\"", "unknown display group")]
    [InlineData("\"max\": 7000", "\"max\": -1", "min must be lower")]
    public void InvalidDisplayIsRejected(string find, string replace, string message)
    {
        using var s = typeof(DefinitionLoader).Assembly.GetManifestResourceStream("MarelliIawEcuStudio.Core.Definitions.IAW-8F-18F.json")!;
        var json = new StreamReader(s).ReadToEnd();
        var i = json.IndexOf(find, StringComparison.Ordinal);
        Assert.True(i >= 0, find);
        var ex = Assert.Throws<DefinitionException>(() => DefinitionLoader.Parse(json[..i] + replace + json[(i + find.Length)..], "x.json"));
        Assert.Contains(message, ex.Message);
    }

    [Theory]
    [InlineData("IAW-04K.P8")]
    [InlineData("IAW 4WF.P8")]
    [InlineData("4WF.P8")]
    [InlineData("iaw-4wf-p8")]
    [InlineData("4wfp8")]
    public void FindsTheCoupeTurboUnitByItsLabelCode(string name) =>
        Assert.Equal("IAW-04K.P8", Registry.Find(name)?.Id);

    [Fact]
    public void UnknownCodeIsNotMatchedByAccident()
    {
        Assert.Null(Registry.Find("IAW 59F"));
        Assert.Null(Registry.Find(""));
        Assert.Equal("4WFP8", EcuDefinition.NormalizeName("IAW 4WF.P8"));
    }
}
