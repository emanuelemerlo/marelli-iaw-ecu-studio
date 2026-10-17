using System.Collections;
using System.Globalization;
using System.Resources;
using System.Text.RegularExpressions;
using MarelliIawEcuStudio.Core.Localization;
using MarelliIawEcuStudio.Presentation.Localization;

namespace MarelliIawEcuStudio.Tests;

/// <summary>Every translation is complete and keeps the placeholders of the English strings.</summary>
public partial class LocalizationTests
{
    private static readonly ResourceManager Resources =
        new("MarelliIawEcuStudio.Core.Localization.Strings", typeof(Localizer).Assembly);

    private static readonly ResourceManager UiResources =
        new("MarelliIawEcuStudio.Presentation.Localization.UiStrings", typeof(Ui).Assembly);

    /// <summary>
    /// Strings that may stay as in English: acronyms, status markers, and words that are
    /// the same in some languages (Dutch "Injector", Polish "Start").
    /// </summary>
    private static readonly HashSet<string> MayEqualEnglish =
    [
        "Act", "Ver", "Sto", "rpm", "ErrRAM", "ErrROM", "ErrEEPROM", "ErrEPROM", "ErrCPU",
        "ErrDigimatic", "StaSigPanOK", "Timeout", "Start", "Stop", "Parameters",
        "Injector", "Injector1", "Injector2", "Injector3", "Injector4", "ErrInj",
    ];

    public static TheoryData<string> Cultures() => new(Localizer.Translations);

    private static Dictionary<string, string> Strings(CultureInfo culture, ResourceManager? resources = null) =>
        (resources ?? Resources).GetResourceSet(culture, createIfNotExists: true, tryParents: false)!
            .Cast<DictionaryEntry>()
            .ToDictionary(e => (string)e.Key, e => (string)e.Value!);

    [GeneratedRegex(@"\{\d+(:[^}]*)?\}|;;")]
    private static partial Regex Tokens();

    [Theory]
    [MemberData(nameof(Cultures))]
    public void TranslationIsCompleteAndConsistent(string code)
    {
        var english = Strings(CultureInfo.InvariantCulture);
        var translated = Strings(CultureInfo.GetCultureInfo(code));

        Assert.Empty(english.Keys.Except(translated.Keys));
        Assert.Empty(translated.Keys.Except(english.Keys));
        foreach (var (key, en) in english)
        {
            var text = translated[key];
            Assert.False(string.IsNullOrWhiteSpace(text), $"{code}/{key} is empty");
            Assert.True(text == text.Trim() && !text.Contains("  "), $"{code}/{key} has stray spaces: '{text}'");
            Assert.Equal(Tokens().Matches(en).Select(m => m.Value).Order(), Tokens().Matches(text).Select(m => m.Value).Order());
            Assert.True(en.StartsWith('&') == text.StartsWith('&'), $"{code}/{key}: keyboard shortcut marker");
            Assert.True(text != en || MayEqualEnglish.Contains(key), $"{code}/{key} is still in English: '{text}'");
        }
    }

    [Fact]
    public void ItalianInterfaceIsCompleteAndConsistent()
    {
        var english = Strings(CultureInfo.InvariantCulture, UiResources);
        var italian = Strings(CultureInfo.GetCultureInfo("it"), UiResources);

        Assert.Empty(english.Keys.Except(italian.Keys));
        Assert.Empty(italian.Keys.Except(english.Keys));
        foreach (var (key, en) in english)
            Assert.Equal(Tokens().Matches(en).Select(m => m.Value).Order(), Tokens().Matches(italian[key]).Select(m => m.Value).Order());
    }

    /// <summary>
    /// Italian follows workshop usage: "stepper", "trimmer CO", "offset", "duty cycle",
    /// "canister" stay as mechanics say them, but these forms are wrong or confusing.
    /// </summary>
    [Theory]
    [InlineData("autoadattamento")] // the Fiat tools say "autoadattività"
    [InlineData("autocalibrazione")]
    [InlineData("toggle")]
    [InlineData("quadro acceso")]   // "chiave su MAR"
    [InlineData("motorino del minimo")]
    public void ItalianAvoidsConfusingTerms(string word)
    {
        var italian = Strings(CultureInfo.GetCultureInfo("it")).Concat(Strings(CultureInfo.GetCultureInfo("it"), UiResources));
        Assert.DoesNotContain(italian, e => e.Value.Contains(word, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Every self-adaptation entry says what adapts, mixture or idle, so the entries can be told apart.</summary>
    [Fact]
    public void ItalianSelfAdaptationNamesSayWhatAdapts()
    {
        var italian = Strings(CultureInfo.GetCultureInfo("it"));
        var named = italian.Where(e => e.Value.StartsWith("Autoadattività", StringComparison.Ordinal) || e.Value.Contains(" autoadattività", StringComparison.Ordinal))
            .Where(e => e.Key != "ErrACParam");
        Assert.NotEmpty(named);
        Assert.All(named, e => Assert.True(e.Value.Contains("miscela", StringComparison.Ordinal) || e.Value.Contains("minimo", StringComparison.Ordinal), $"{e.Key}: '{e.Value}'"));
        Assert.Equal(italian.Count(e => e.Key.StartsWith("FABE", StringComparison.Ordinal)),
            italian.Where(e => e.Key.StartsWith("FABE", StringComparison.Ordinal)).Select(e => e.Value).Distinct().Count());
    }

    [Fact]
    public void LanguageNamesAreWrittenAsMenuEntries()
    {
        Assert.All(Ui.Languages, l => Assert.True(char.IsUpper(l.Name[0]), l.Name));
        Assert.Equal("Italiano", Ui.Languages.Single(l => l.Code == "it").Name);
        Assert.Equal("Srpski", Ui.Languages.Single(l => l.Code == "sr").Name); // Latin, like the translation
    }

    [Theory]
    [InlineData("toggle")]
    [InlineData("onebyte")]
    [InlineData("code")]
    public void EveryAdjustmentTypeHasAName(string type)
    {
        Assert.True(UiResources.GetString("AdjustType_" + type, CultureInfo.InvariantCulture) is { Length: > 0 });
        Assert.NotEqual(UiResources.GetString("AdjustType_" + type, CultureInfo.InvariantCulture), UiResources.GetString("AdjustType_" + type, CultureInfo.GetCultureInfo("it")));
    }
}
