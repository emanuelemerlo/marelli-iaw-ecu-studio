using System.Globalization;
using MarelliIawEcuStudio.Core.Definitions;
using MarelliIawEcuStudio.Core.Localization;

namespace MarelliIawEcuStudio.Core.Decoding;

/// <summary>State of one diagnostic trouble code.</summary>
public sealed record ErrorState(ErrorDefinition Definition, bool IsActive, bool IsVerified, bool IsStored, string Reason)
{
    /// <summary>The error is present in at least one of the three states.</summary>
    public bool IsPresent => IsActive || IsVerified || IsStored;

    public string GetName(CultureInfo? culture = null) => Localizer.Text(Definition.NameKey, Definition.Name, culture);

    /// <summary>Status letters as shown by IES2 (for example "A" + "M" for active and memorised).</summary>
    public string GetStatusText(CultureInfo? culture = null) =>
        (IsActive ? Localizer.Get("Act", culture) : "") +
        (IsVerified ? Localizer.Get("Ver", culture) : "") +
        (IsStored ? Localizer.Get("Sto", culture) : "");
}

/// <summary>
/// Decodes error registers. The schemes reproduce the per-family decoding
/// functions of IES2 (<c>err16F</c>, <c>err18F</c>, <c>err04K</c>, <c>err8F_68</c>, <c>errCODE</c>).
/// </summary>
public static class ErrorDecoder
{
    public const string StoredFirst = "storedFirst";
    public const string ActiveVerifiedStored = "activeVerifiedStored";
    public const string ActiveVerifiedStoredNoVerifiedFlag = "activeVerifiedStoredNoVerifiedFlag";
    public const string NoReason = "noReason";
    public const string FiatCode = "fiatCode";

    public static bool IsKnownScheme(string scheme) =>
        scheme is StoredFirst or ActiveVerifiedStored or ActiveVerifiedStoredNoVerifiedFlag or NoReason or FiatCode;

    public static ErrorState Decode(string scheme, ErrorDefinition e, RegisterBuffer b, CultureInfo? culture = null)
    {
        var active = b.GetBit(e.Active, e.Bit);
        var stored = b.GetBit(e.Stored, e.Bit);
        string Reason(bool high) => high
            ? Localizer.Text(e.HighReasonKey, e.HighReason, culture)
            : Localizer.Text(e.LowReasonKey, e.LowReason, culture);

        switch (scheme)
        {
            case StoredFirst:
            {
                // IES2 err16F: the reason bit is taken from the "stored" extension
                // register when the error is stored, otherwise from the "active" one.
                var high = stored ? b.GetBit(e.Ext.Stored, e.Ext.Bit) : b.GetBit(e.Ext.Active, e.Ext.Bit);
                return new ErrorState(e, active, false, stored, Reason(high));
            }
            case ActiveVerifiedStored:
            case ActiveVerifiedStoredNoVerifiedFlag:
            {
                // IES2 err18F / err04K: priority active > verified > stored.
                var verified = b.GetBit(e.Verified, e.Bit);
                var reason = "";
                if (e.Ext.Active != 0)
                {
                    var high = active ? b.GetBit(e.Ext.Active, e.Ext.Bit)
                        : verified ? b.GetBit(e.Ext.Verified, e.Ext.Bit)
                        : b.GetBit(e.Ext.Stored, e.Ext.Bit);
                    reason = Reason(high);
                }
                return new ErrorState(e, active, scheme == ActiveVerifiedStored && verified, stored, reason);
            }
            case NoReason:
                return new ErrorState(e, active, false, stored, "");
            case FiatCode:
                return new ErrorState(e, active, false, stored, "FIAT CODE");
            default:
                throw new ArgumentException($"unknown error scheme '{scheme}'", nameof(scheme));
        }
    }

    public static IReadOnlyList<ErrorState> DecodeAll(ErrorGroup group, RegisterBuffer buffer, CultureInfo? culture = null) =>
        [.. group.Items.Select(e => Decode(group.Scheme, e, buffer, culture))];
}
