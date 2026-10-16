using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using MarelliIawEcuStudio.Core.Decoding;
using MarelliIawEcuStudio.Core.Protocol;

namespace MarelliIawEcuStudio.Core.Definitions;

/// <summary>A problem found while loading a definition file.</summary>
public sealed record DefinitionIssue(string Origin, string Message)
{
    public override string ToString() => $"{Origin}: {Message}";
}

/// <summary>Reads and validates ECU definition files.</summary>
public static class DefinitionLoader
{
    private const string ResourcePrefix = "MarelliIawEcuStudio.Core.Definitions.";

    public static readonly JsonSerializerOptions JsonOptions = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip,
            WriteIndented = true,
        };
        options.Converters.Add(new HexByteConverter());
        options.Converters.Add(new HexByteArrayConverter());
        return options;
    }

    /// <summary>Parses and validates one definition from JSON text.</summary>
    /// <exception cref="DefinitionException">The text is not a valid definition.</exception>
    public static EcuDefinition Parse(string json, string origin)
    {
        EcuDefinition? definition;
        try
        {
            definition = JsonSerializer.Deserialize<EcuDefinition>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new DefinitionException([new DefinitionIssue(origin, ex.Message)]);
        }
        if (definition is null)
            throw new DefinitionException([new DefinitionIssue(origin, "empty document")]);
        definition.Origin = origin;
        var issues = Validate(definition).ToList();
        if (issues.Count > 0)
            throw new DefinitionException(issues);
        return definition;
    }

    /// <summary>Loads the definitions shipped inside this assembly.</summary>
    public static IReadOnlyList<EcuDefinition> LoadBuiltIn()
    {
        var assembly = typeof(DefinitionLoader).Assembly;
        var result = new List<EcuDefinition>();
        foreach (var name in assembly.GetManifestResourceNames().Where(n => n.StartsWith(ResourcePrefix, StringComparison.Ordinal)).Order(StringComparer.Ordinal))
        {
            using var stream = assembly.GetManifestResourceStream(name)!;
            using var reader = new StreamReader(stream);
            result.Add(Parse(reader.ReadToEnd(), "built-in:" + name[ResourcePrefix.Length..]));
        }
        return result;
    }

    /// <summary>
    /// Loads every <c>*.json</c> file in <paramref name="directory"/>. Invalid
    /// files are skipped and reported in <paramref name="issues"/> so a single
    /// broken community file never prevents the application from starting.
    /// </summary>
    public static IReadOnlyList<EcuDefinition> LoadDirectory(string directory, ICollection<DefinitionIssue> issues)
    {
        var result = new List<EcuDefinition>();
        if (!Directory.Exists(directory))
            return result;
        foreach (var file in Directory.EnumerateFiles(directory, "*.json").Order(StringComparer.Ordinal))
        {
            try
            {
                result.Add(Parse(File.ReadAllText(file), file));
            }
            catch (DefinitionException ex)
            {
                foreach (var issue in ex.Issues)
                    issues.Add(issue);
            }
            catch (IOException ex)
            {
                issues.Add(new DefinitionIssue(file, ex.Message));
            }
        }
        return result;
    }

    /// <summary>Checks a definition for consistency errors.</summary>
    public static IEnumerable<DefinitionIssue> Validate(EcuDefinition d)
    {
        var origin = d.Origin ?? d.Id;
        DefinitionIssue Issue(string message) => new(origin, message);

        if (string.IsNullOrWhiteSpace(d.Id))
            yield return Issue("id is required");
        if (!IdentificationProfile.TryGet(d.Protocol.Identification, out _))
            yield return Issue($"unknown identification profile '{d.Protocol.Identification}'");
        if (d.Detection.AutoDetect && d.Detection.IsoMatches.Count == 0)
            yield return Issue("autoDetect requires at least one isoMatches entry");

        foreach (var (name, table) in d.Tables)
            if (table.Length != 256)
                yield return Issue($"table '{name}' must have 256 values, has {table.Length}");

        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var p in d.Parameters)
        {
            var where = $"parameter '{p.Id}'";
            if (!ids.Add(p.Id))
                yield return Issue($"{where}: duplicate id");
            if (p.NameKey is null && p.Name is null)
                yield return Issue($"{where}: nameKey or name is required");
            var expected = p.Decode.Raw switch
            {
                "u8" or "s8" or "bit" => 1,
                "u16" or "s16" => 2,
                _ => -1,
            };
            if (expected < 0)
                yield return Issue($"{where}: unknown raw type '{p.Decode.Raw}'");
            else if (p.Requests.Length != expected)
                yield return Issue($"{where}: raw '{p.Decode.Raw}' needs {expected} request byte(s), has {p.Requests.Length}");
            if (p.Decode.Raw == "bit" && p.Decode.Bit is < 0 or > 7)
                yield return Issue($"{where}: bit must be 0-7");
            if (p.Decode.Expr is not null && p.Decode.Table is not null)
                yield return Issue($"{where}: use either expr or table, not both");
            if (p.Decode.Table is not null && !d.Tables.ContainsKey(p.Decode.Table))
                yield return Issue($"{where}: unknown table '{p.Decode.Table}'");
            if (p.Decode.Table is not null && p.Decode.Raw != "u8")
                yield return Issue($"{where}: tables require raw 'u8'");
            if (p.Decode.Expr is not null)
            {
                string? error = null;
                try
                {
                    Expression.Parse(p.Decode.Expr);
                }
                catch (FormatException ex)
                {
                    error = ex.Message;
                }
                if (error is not null)
                    yield return Issue($"{where}: {error}");
            }
            if (p.Display is { } display)
            {
                if (!DisplaySpec.Groups.Contains(display.Group))
                    yield return Issue($"{where}: unknown display group '{display.Group}'");
                if (!DisplaySpec.Widgets.Contains(display.Widget))
                    yield return Issue($"{where}: unknown display widget '{display.Widget}'");
                if (display.Widget is DisplaySpec.WidgetGauge or DisplaySpec.WidgetBar && display.Min >= display.Max)
                    yield return Issue($"{where}: display min must be lower than max");
                if (display.Widget == DisplaySpec.WidgetLed && p.Decode.Raw != "bit")
                    yield return Issue($"{where}: led needs a bit parameter");
                if (display.Led is not ("good" or "warning" or "info"))
                    yield return Issue($"{where}: led colour must be good, warning or info");
                foreach (var z in display.Zones)
                    if (z.From >= z.To || z.Level is not ("cold" or "warning" or "danger"))
                        yield return Issue($"{where}: invalid display zone {z.From}..{z.To} '{z.Level}'");
            }
            if (p.Format is not null)
            {
                string? error = null;
                try
                {
                    _ = 1.5m.ToString(p.Format, CultureInfo.InvariantCulture);
                }
                catch (FormatException ex)
                {
                    error = ex.Message;
                }
                if (error is not null)
                    yield return Issue($"{where}: invalid format '{p.Format}': {error}");
            }
        }

        foreach (var (groupName, group) in new[] { ("engine", d.Errors.Engine), ("immobilizer", d.Errors.Immobilizer) })
        {
            if (!ErrorDecoder.IsKnownScheme(group.Scheme))
                yield return Issue($"errors.{groupName}: unknown scheme '{group.Scheme}'");
            foreach (var e in group.Items)
                if (e.Bit is < 0 or > 7 || e.Ext.Bit is < 0 or > 7)
                    yield return Issue($"errors.{groupName} '{e.NameKey ?? e.Name}': bits must be 0-7");
        }

        foreach (var t in d.ActiveTests.Append(d.ClearCodes).OfType<ActiveTestDefinition>())
            if (t.Requests.Length == 0)
                yield return Issue($"active test '{t.NameKey ?? t.Name}': requests must not be empty");

        foreach (var a in d.Adjustments)
            if (a.Type is not ("toggle" or "onebyte" or "code"))
                yield return Issue($"adjustment '{a.NameKey ?? a.Name}': unknown type '{a.Type}'");
    }
}

public sealed class DefinitionException(IReadOnlyList<DefinitionIssue> issues)
    : Exception(string.Join(Environment.NewLine, issues))
{
    public IReadOnlyList<DefinitionIssue> Issues { get; } = issues;
}

/// <summary>Reads bytes written either as numbers or as <c>"0x2A"</c> strings.</summary>
internal sealed class HexByteConverter : JsonConverter<byte>
{
    public override byte Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number)
            return reader.GetByte();
        if (reader.TokenType == JsonTokenType.String)
            return ParseByte(reader.GetString()!);
        throw new JsonException("expected a byte as number or \"0xNN\" string");
    }

    public override void Write(Utf8JsonWriter writer, byte value, JsonSerializerOptions options) =>
        writer.WriteStringValue($"0x{value:X2}");

    internal static byte ParseByte(string s)
    {
        var ok = s.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? byte.TryParse(s.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var v)
            : byte.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out v);
        return ok ? v : throw new JsonException($"'{s}' is not a byte value");
    }
}

/// <summary>Reads byte arrays as JSON arrays of bytes instead of the default base64.</summary>
internal sealed class HexByteArrayConverter : JsonConverter<byte[]>
{
    public override byte[]? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;
        if (reader.TokenType != JsonTokenType.StartArray)
            throw new JsonException("expected an array of bytes");
        var bytes = new List<byte>();
        var item = new HexByteConverter();
        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
            bytes.Add(item.Read(ref reader, typeof(byte), options));
        return [.. bytes];
    }

    public override void Write(Utf8JsonWriter writer, byte[] value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        foreach (var b in value)
            writer.WriteStringValue($"0x{b:X2}");
        writer.WriteEndArray();
    }
}
