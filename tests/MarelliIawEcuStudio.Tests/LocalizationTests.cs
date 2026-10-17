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
}
