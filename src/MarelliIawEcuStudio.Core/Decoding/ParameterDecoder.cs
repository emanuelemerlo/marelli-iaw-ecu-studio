using System.Globalization;
using MarelliIawEcuStudio.Core.Definitions;
using MarelliIawEcuStudio.Core.Localization;

namespace MarelliIawEcuStudio.Core.Decoding;

/// <summary>A parameter definition prepared for fast, repeated decoding.</summary>
public sealed class ParameterDecoder
{
    private readonly Func<RegisterBuffer, int> _raw;
    private readonly Func<double, double> _convert;

    public ParameterDecoder(ParameterDefinition definition, IReadOnlyDictionary<string, double[]> tables)
    {
        Definition = definition;
        var spec = definition.Decode;
        var r = definition.Requests;
        _raw = spec.Raw switch
        {
            "u8" => b => b[r[0]],
            "s8" => b => unchecked((sbyte)b[r[0]]),
            "u16" => b => (b[r[0]] << 8) | b[r[1]],
            "s16" => b => unchecked((short)((b[r[0]] << 8) | b[r[1]])),
            "bit" => spec.Invert
                ? b => b.GetBit(r[0], spec.Bit) ? 0 : 1
                : b => b.GetBit(r[0], spec.Bit) ? 1 : 0,
            _ => throw new ArgumentException($"unknown raw type '{spec.Raw}'", nameof(definition)),
        };
        if (spec.Table is not null)
        {
            var table = tables[spec.Table];
            _convert = x => table[(int)x];
        }
        else if (spec.Expr is not null)
        {
            var expression = Expression.Parse(spec.Expr);
            _convert = expression.Evaluate;
        }
        else
        {
            _convert = x => x;
        }
    }

    public ParameterDefinition Definition { get; }

    public string Id => Definition.Id;

    public IReadOnlyList<byte> Requests => Definition.Requests;

    public string GetName(CultureInfo? culture = null) => Localizer.Text(Definition.NameKey, Definition.Name, culture);

    public string GetUnit(CultureInfo? culture = null) => Localizer.Text(Definition.UnitKey, Definition.Unit, culture);

    public string GetFormat(CultureInfo? culture = null) =>
        Definition.FormatKey is not null ? Localizer.Get(Definition.FormatKey, culture) : Definition.Format ?? "0.##";

    /// <summary>The raw integer read from the registers, before conversion.</summary>
    public int ReadRaw(RegisterBuffer buffer) => _raw(buffer);

    /// <summary>
    /// The physical value. Returns null when the formula gives no finite
    /// number (for example a division by zero in a community definition).
    /// </summary>
    public decimal? Decode(RegisterBuffer buffer)
    {
        var value = _convert(_raw(buffer));
        if (!double.IsFinite(value) || Math.Abs(value) > (double)decimal.MaxValue)
            return null;
        return (decimal)value;
    }

    /// <summary>True when every register of this parameter was read successfully.</summary>
    public bool IsValid(RegisterBuffer buffer) => Definition.Requests.All(buffer.IsValid);

    /// <summary>The value formatted for display, or <c>#ERR</c> when the last read failed.</summary>
    public string Format(RegisterBuffer buffer, CultureInfo? culture = null)
    {
        if (!buffer.IsValid(Definition.Requests[0]))
            return "#ERR";
        var value = Decode(buffer);
        return value is null ? "#ERR" : value.Value.ToString(GetFormat(culture), culture ?? CultureInfo.CurrentCulture);
    }
}
