using System.Text.Json.Serialization;

namespace MarelliIawEcuStudio.Core.Definitions;

/// <summary>
/// Everything the application knows about one ECU family: how to identify it,
/// which live parameters it exposes, how its error registers are laid out and
/// which active commands it accepts. Loaded from a JSON definition file.
/// </summary>
public sealed class EcuDefinition
{
    /// <summary>Unique identifier, for example <c>IAW-6F/16F</c>.</summary>
    public required string Id { get; init; }

    /// <summary>Short display name.</summary>
    public required string Name { get; init; }

    /// <summary>Long display name, for example <c>Magneti-Marelli IAW-6F/16F SPI</c>.</summary>
    public string? LongName { get; init; }

    /// <summary>Where the data comes from (original module, capture, documentation).</summary>
    public string? Source { get; init; }

    /// <summary>
    /// True when the definition has been checked against a real ECU or against
    /// the original IES2 code. Active commands of unverified definitions are
    /// disabled unless the user explicitly allows them.
    /// </summary>
    public bool Verified { get; init; }

    public ProtocolSettings Protocol { get; init; } = new();

    /// <summary>Technology, period and compatible vehicles of the family.</summary>
    public FamilyInfo? Family { get; init; }

    /// <summary>
    /// Other names or hardware codes of units that work with this definition
    /// (for example IAW 4WF.P8 for the IAW-04K.P8 family), so users find
    /// their ECU by the code printed on its label.
    /// </summary>
    public IReadOnlyList<EcuAlias> Aliases { get; init; } = [];

    /// <summary>True when <paramref name="name"/> is the id, the name or an alias, ignoring case, spaces, dashes and dots.</summary>
    public bool IsKnownAs(string name)
    {
        var key = NormalizeName(name);
        return key.Length > 0 && (NormalizeName(Id) == key || NormalizeName(Name) == key || Aliases.Any(a => NormalizeName(a.Name) == key));
    }

    /// <summary>"IAW 4WF.P8", "iaw-4wf-p8" and "4WF.P8" all become "4WFP8".</summary>
    public static string NormalizeName(string name)
    {
        var compact = new string([.. name.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant)]);
        return compact.StartsWith("IAW", StringComparison.Ordinal) ? compact[3..] : compact;
    }

    public DetectionRules Detection { get; init; } = new();

    public IReadOnlyList<VehicleEntry> Vehicles { get; init; } = [];

    /// <summary>Named lookup tables of 256 values, referenced by <see cref="DecodeSpec.Table"/>.</summary>
    public IReadOnlyDictionary<string, double[]> Tables { get; init; } = new Dictionary<string, double[]>();

    public IReadOnlyList<ParameterDefinition> Parameters { get; init; } = [];

    public ErrorSection Errors { get; init; } = new();

    public IReadOnlyList<ActiveTestDefinition> ActiveTests { get; init; } = [];

    public ActiveTestDefinition? ClearCodes { get; init; }

    public IReadOnlyList<AdjustmentDefinition> Adjustments { get; init; } = [];

    /// <summary>The file or resource this definition was loaded from.</summary>
    [JsonIgnore]
    public string? Origin { get; internal set; }

    public override string ToString() => Id;
}

public sealed class ProtocolSettings
{
    /// <summary>
    /// Identification profile: <c>standard</c> (slow init, ISO at 0x2A-0x2F,
    /// CODRIC at 0x17-0x20) or <c>p8</c> (no init, ISO at 0x30-0x35, CODRIC at 0x36-0x3F).
    /// </summary>
    public string Identification { get; init; } = "standard";
}

public sealed class DetectionRules
{
    /// <summary>Whether the family takes part in automatic detection.</summary>
    public bool AutoDetect { get; init; }

    /// <summary>Order in which families are tried; lower first.</summary>
    public int Order { get; init; }

    /// <summary>The family matches when any of these substrings is found in the ISO code.</summary>
    public IReadOnlyList<CodeMatch> IsoMatches { get; init; } = [];

    /// <summary>Informational: CODRIC prefixes seen for this family.</summary>
    public IReadOnlyList<CodeMatch> CodricMatches { get; init; } = [];
}

public sealed record CodeMatch(int Offset, string Value)
{
    public bool IsMatch(string? code) =>
        code is not null && code.Length >= Offset + Value.Length &&
        string.CompareOrdinal(code, Offset, Value, 0, Value.Length) == 0;
}

public sealed record VehicleEntry(string Iso, string Model);

/// <summary>Another name of an ECU covered by a definition; <c>Note</c> says where it is fitted.</summary>
public sealed record EcuAlias(string Name, string? Note);

/// <summary>A live data value read from one or two ECU registers.</summary>
public sealed class ParameterDefinition
{
    public required string Id { get; init; }

    /// <summary>Localisation key of the parameter name.</summary>
    public string? NameKey { get; init; }

    /// <summary>Literal name, used when no key is given.</summary>
    public string? Name { get; init; }

    public string? UnitKey { get; init; }

    public string? Unit { get; init; }

    /// <summary>.NET numeric format string, for example <c>0.00</c>.</summary>
    public string? Format { get; init; }

    /// <summary>Localisation key of the format string (used for YES/NO flags).</summary>
    public string? FormatKey { get; init; }

    /// <summary>Request bytes; each is also the register the response is stored in.</summary>
    public required byte[] Requests { get; init; }

    public required DecodeSpec Decode { get; init; }

    /// <summary>Dashboard group and instrument; optional.</summary>
    public DisplaySpec? Display { get; init; }

    public DisplaySpec EffectiveDisplay => Display ?? DisplaySpec.DefaultFor(this);
}

public sealed class DecodeSpec
{
    /// <summary><c>u8</c>, <c>s8</c>, <c>u16</c>, <c>s16</c> (big endian over the two requests) or <c>bit</c>.</summary>
    public required string Raw { get; init; }

    /// <summary>Bit index (0-7) for <c>raw = bit</c>.</summary>
    public int Bit { get; init; }

    /// <summary>For <c>raw = bit</c>: report 1 when the bit is clear.</summary>
    public bool Invert { get; init; }

    /// <summary>Optional expression in <c>x</c>; see <see cref="Decoding.Expression"/>.</summary>
    public string? Expr { get; init; }

    /// <summary>Optional lookup table name; the raw value indexes the table.</summary>
    public string? Table { get; init; }
}

public sealed class ErrorSection
{
    public ErrorGroup Engine { get; init; } = new();

    public ErrorGroup Immobilizer { get; init; } = new();
}

public sealed class ErrorGroup
{
    /// <summary>
    /// How flags and reasons are derived: <c>storedFirst</c>, <c>activeVerifiedStored</c>,
    /// <c>activeVerifiedStoredNoVerifiedFlag</c>, <c>noReason</c> or <c>fiatCode</c>.
    /// </summary>
    public string Scheme { get; init; } = "noReason";

    /// <summary>Registers to read before decoding this group.</summary>
    public byte[] Requests { get; init; } = [];

    public IReadOnlyList<ErrorDefinition> Items { get; init; } = [];
}

/// <summary>One diagnostic trouble code, described by bit positions in error registers.</summary>
public sealed class ErrorDefinition
{
    public string? NameKey { get; init; }

    public string? Name { get; init; }

    /// <summary>Register holding the "active" flags.</summary>
    public byte Active { get; init; }

    /// <summary>Register holding the "verified" flags (0 when not available).</summary>
    public byte Verified { get; init; }

    /// <summary>Register holding the "stored" flags.</summary>
    public byte Stored { get; init; }

    /// <summary>Bit of this error in the three flag registers.</summary>
    public int Bit { get; init; }

    /// <summary>Registers and bit that tell which of the two reasons applies.</summary>
    public ErrorExtension Ext { get; init; } = new();

    public string? HighReasonKey { get; init; }

    public string? HighReason { get; init; }

    public string? LowReasonKey { get; init; }

    public string? LowReason { get; init; }
}

public sealed class ErrorExtension
{
    public byte Active { get; init; }

    public byte Verified { get; init; }

    public byte Stored { get; init; }

    public int Bit { get; init; }
}

/// <summary>An actuator test, or the "clear codes" command.</summary>
public sealed class ActiveTestDefinition
{
    public string? NameKey { get; init; }

    public string? Name { get; init; }

    /// <summary>True when the engine must be running for the test.</summary>
    public bool EngineRunning { get; init; }

    /// <summary>Maximum time the ECU needs to report the result.</summary>
    public int TimeoutSeconds { get; init; }

    public required byte[] Requests { get; init; }
}

public sealed class AdjustmentDefinition
{
    public string? NameKey { get; init; }

    public string? Name { get; init; }

    /// <summary>Register with the current value, read before the adjustment (0 = none).</summary>
    public byte StatusRequest { get; init; }

    public byte StatusMask { get; init; }

    /// <summary>Codes sent after entering active mode.</summary>
    public byte[] PreSet { get; init; } = [];

    /// <summary>Codes sent after the value (if any).</summary>
    public byte[]? PostSet { get; init; }

    /// <summary><c>toggle</c>, <c>onebyte</c> or <c>code</c>.</summary>
    public string Type { get; init; } = "toggle";
}
