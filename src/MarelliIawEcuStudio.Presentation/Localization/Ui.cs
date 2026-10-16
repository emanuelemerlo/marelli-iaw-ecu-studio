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
        [.. new[] { "en" }.Concat(Localizer.Translations).Select(c => new LanguageOption(c, CultureInfo.GetCultureInfo(c).NativeName))];

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
