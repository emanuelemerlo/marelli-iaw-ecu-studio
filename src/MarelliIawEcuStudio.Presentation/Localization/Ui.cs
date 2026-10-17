using System.ComponentModel;
using System.Globalization;
using System.Resources;
using MarelliIawEcuStudio.Core.Localization;

namespace MarelliIawEcuStudio.Presentation.Localization;

/// <summary>
/// Texts of the user interface, bindable by key (<c>{Binding Ui[Connect]}</c>).
/// Keys not in the UI resources fall back to the IES2 strings of the Core,
/// which are translated into ten languages. Raises a change notification
/// when the language changes so every bound text updates.
/// </summary>
public sealed class Ui : INotifyPropertyChanged
{
    private static readonly ResourceManager Resources =
        new("MarelliIawEcuStudio.Presentation.Localization.UiStrings", typeof(Ui).Assembly);

    public static Ui Instance { get; } = new();

    /// <summary>Languages offered in the UI: English plus the IES2 translations.</summary>
    public static IReadOnlyList<LanguageOption> Languages { get; } =
        [.. new[] { "en" }.Concat(Localizer.Translations).Select(c => new LanguageOption(c, NativeName(c)))];

    /// <summary>
    /// The name of a language in that language, capitalised as a menu entry. The
    /// operating system's names are not used: some are lower case ("italiano",
    /// "español"), and Serbian comes out in Cyrillic while the translation is Latin.
    /// </summary>
    public static string NativeName(string code) => code switch
    {
        "en" => "English",
        "de" => "Deutsch",
        "es" => "Español",
        "hr" => "Hrvatski",
        "hu" => "Magyar",
        "it" => "Italiano",
        "nl" => "Nederlands",
        "pl" => "Polski",
        "pt" => "Português",
        "sr" => "Srpski",
        _ => CultureInfo.GetCultureInfo(code) is var c && c.NativeName.Length > 0
            ? char.ToUpper(c.NativeName[0], c) + c.NativeName[1..]
            : code,
    };

    public event PropertyChangedEventHandler? PropertyChanged;

    public string this[string key] => Get(key);

    public static string Get(string key)
    {
        var culture = CultureInfo.CurrentUICulture;
        return Resources.GetString(key, culture) ?? (Localizer.TryGet(key, out var core, culture) ? core : key);
    }

    public static string Format(string key, params object?[] args) => string.Format(CultureInfo.CurrentCulture, Get(key), args);

    /// <summary>Switches the language of both UI and Core strings.</summary>
    public void SetLanguage(string cultureName)
    {
        var culture = CultureInfo.GetCultureInfo(cultureName == "en" ? "" : cultureName);
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
    }
}

public sealed record LanguageOption(string Code, string Name)
{
    public override string ToString() => Name;
}

/// <summary>Colour theme choice; the name follows the UI language.</summary>
public sealed record ThemeOption(string Code)
{
    public const string System = "system";
    public const string Light = "light";
    public const string Dark = "dark";

    public static IReadOnlyList<string> Codes { get; } = [System, Light, Dark];

    public string Name => Ui.Get("Theme_" + Code);

    /// <summary>The following choice, wrapping around: system → light → dark → system.</summary>
    public ThemeOption Next()
    {
        var i = Codes.ToList().IndexOf(Code);
        return new ThemeOption(Codes[(i + 1) % Codes.Count]);
    }

    public override string ToString() => Name;
}
