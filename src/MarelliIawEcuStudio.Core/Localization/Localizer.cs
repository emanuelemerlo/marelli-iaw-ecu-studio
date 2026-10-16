using System.Globalization;
using System.Resources;

namespace MarelliIawEcuStudio.Core.Localization;

/// <summary>
/// Looks up user-visible strings by key. The strings and the ten
/// translations come from the original IES2 resources.
/// </summary>
public static class Localizer
{
    private static readonly ResourceManager Resources =
        new("MarelliIawEcuStudio.Core.Localization.Strings", typeof(Localizer).Assembly);

    /// <summary>Cultures with a translation, besides the English default.</summary>
    public static IReadOnlyList<string> Translations { get; } = ["de", "es", "hr", "hu", "it", "nl", "pl", "pt", "sr"];

    /// <summary>Returns the string for <paramref name="key"/>, or the key itself when it is missing.</summary>
    public static string Get(string key, CultureInfo? culture = null) =>
        TryGet(key, out var value, culture) ? value : key;

    public static bool TryGet(string key, out string value, CultureInfo? culture = null)
    {
        value = Resources.GetString(key, culture ?? CultureInfo.CurrentUICulture)!;
        return value is not null;
    }

    /// <summary>Resolves a key/literal pair as used in definition files.</summary>
    public static string Text(string? key, string? literal, CultureInfo? culture = null) =>
        key is not null ? Get(key, culture) : literal ?? string.Empty;
}
