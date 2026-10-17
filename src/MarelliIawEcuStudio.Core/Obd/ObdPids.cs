using System.Globalization;

namespace MarelliIawEcuStudio.Core.Obd;

/// <summary>
/// A mode 01 parameter of SAE J1979 / ISO 15031-5. <c>A</c>, <c>B</c>... are
/// the data bytes of the answer, as in the standard.
/// </summary>
public sealed record ObdPid(byte Pid, string Id, string Name, string NameIt, string Unit, int Bytes, string Format, Func<byte[], double> Decode)
{
    public string GetName(CultureInfo? culture = null) =>
        (culture ?? CultureInfo.CurrentUICulture).TwoLetterISOLanguageName == "it" ? NameIt : Name;

    public string FormatValue(double value, CultureInfo? culture = null) => value.ToString(Format, culture ?? CultureInfo.CurrentCulture);
}

/// <summary>The standard mode 01 PIDs that petrol cars of the EOBD era (2001 on) commonly support.</summary>
public static class ObdPids
{
    private static double A(byte[] d) => d[0];

    private static double Ab(byte[] d) => (d[0] << 8) | d[1];

    private static double Percent(byte[] d) => d[0] * 100.0 / 255;

    private static double Trim(byte[] d) => (d[0] - 128) * 100.0 / 128;

    public static IReadOnlyList<ObdPid> All { get; } =
    [
        new(0x04, "LOAD", "Calculated engine load", "Carico motore calcolato", "%", 1, "0.0", Percent),
        new(0x05, "ECT", "Coolant temperature", "Temperatura liquido refrigerante", "°C", 1, "0", d => A(d) - 40),
        new(0x06, "STFT1", "Short term fuel trim, bank 1", "Correzione carburante breve termine, banco 1", "%", 1, "0.0", Trim),
        new(0x07, "LTFT1", "Long term fuel trim, bank 1", "Correzione carburante lungo termine, banco 1", "%", 1, "0.0", Trim),
        new(0x08, "STFT2", "Short term fuel trim, bank 2", "Correzione carburante breve termine, banco 2", "%", 1, "0.0", Trim),
        new(0x09, "LTFT2", "Long term fuel trim, bank 2", "Correzione carburante lungo termine, banco 2", "%", 1, "0.0", Trim),
        new(0x0A, "FP", "Fuel pressure", "Pressione carburante", "kPa", 1, "0", d => A(d) * 3),
        new(0x0B, "MAP", "Intake manifold pressure", "Pressione collettore aspirazione", "kPa", 1, "0", A),
        new(0x0C, "RPM", "Engine speed", "Regime motore", "rpm", 2, "0", d => Ab(d) / 4),
        new(0x0D, "VSS", "Vehicle speed", "Velocità veicolo", "km/h", 1, "0", A),
        new(0x0E, "SPARKADV", "Ignition advance", "Anticipo accensione", "°", 1, "0.0", d => A(d) / 2 - 64),
        new(0x0F, "IAT", "Intake air temperature", "Temperatura aria aspirata", "°C", 1, "0", d => A(d) - 40),
        new(0x10, "MAF", "Air flow rate", "Portata aria", "g/s", 2, "0.00", d => Ab(d) / 100),
        new(0x11, "TP", "Throttle position", "Posizione farfalla", "%", 1, "0.0", Percent),
        new(0x14, "O2S11", "Oxygen sensor 1, bank 1", "Sonda lambda 1, banco 1", "V", 2, "0.000", d => A(d) / 200),
        new(0x15, "O2S12", "Oxygen sensor 2, bank 1", "Sonda lambda 2, banco 1", "V", 2, "0.000", d => A(d) / 200),
        new(0x16, "O2S13", "Oxygen sensor 3, bank 1", "Sonda lambda 3, banco 1", "V", 2, "0.000", d => A(d) / 200),
        new(0x17, "O2S14", "Oxygen sensor 4, bank 1", "Sonda lambda 4, banco 1", "V", 2, "0.000", d => A(d) / 200),
        new(0x1F, "RUNTM", "Time since engine start", "Tempo dall'avviamento", "s", 2, "0", Ab),
        new(0x21, "MIL_DIST", "Distance with MIL on", "Distanza con spia MIL accesa", "km", 2, "0", Ab),
        new(0x2F, "FLI", "Fuel level", "Livello carburante", "%", 1, "0.0", Percent),
        new(0x30, "WARM_UPS", "Warm-ups since codes cleared", "Riscaldamenti da cancellazione errori", "", 1, "0", A),
        new(0x31, "CLR_DIST", "Distance since codes cleared", "Distanza da cancellazione errori", "km", 2, "0", Ab),
        new(0x33, "BARO", "Barometric pressure", "Pressione barometrica", "kPa", 1, "0", A),
        new(0x42, "VPWR", "Control module voltage", "Tensione centralina", "V", 2, "0.00", d => Ab(d) / 1000),
        new(0x43, "LOAD_ABS", "Absolute load", "Carico assoluto", "%", 2, "0.0", d => Ab(d) * 100 / 255),
        new(0x44, "LAMBDA", "Commanded equivalence ratio", "Rapporto lambda comandato", "λ", 2, "0.000", d => Ab(d) / 32768),
        new(0x45, "TP_R", "Relative throttle position", "Posizione farfalla relativa", "%", 1, "0.0", Percent),
        new(0x46, "AAT", "Ambient air temperature", "Temperatura aria esterna", "°C", 1, "0", d => A(d) - 40),
        new(0x49, "APP_D", "Accelerator pedal position D", "Posizione pedale acceleratore D", "%", 1, "0.0", Percent),
    ];

    private static readonly Dictionary<byte, ObdPid> ByPid = All.ToDictionary(p => p.Pid);

    public static ObdPid? Get(byte pid) => ByPid.GetValueOrDefault(pid);

    /// <summary>Finds a PID by id (RPM), hex number (0C or 0x0C) or name, ignoring case.</summary>
    public static ObdPid? Find(string key)
    {
        var k = key.Trim();
        var hex = k.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? k[2..] : k;
        if (hex.Length <= 2 && byte.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var n) && ByPid.TryGetValue(n, out var byNumber))
            return byNumber;
        return All.FirstOrDefault(p => string.Equals(p.Id, k, StringComparison.OrdinalIgnoreCase) || string.Equals(p.Name, k, StringComparison.OrdinalIgnoreCase));
    }
}

/// <summary>A diagnostic trouble code as defined by SAE J2012: P0301, C1234...</summary>
public readonly record struct Dtc(ushort Raw)
{
    public string Code
    {
        get
        {
            var letter = "PCBU"[Raw >> 14];
            return $"{letter}{(Raw >> 12) & 0x3}{(Raw >> 8) & 0xF:X}{(Raw >> 4) & 0xF:X}{Raw & 0xF:X}";
        }
    }

    /// <summary>
    /// True for codes with a standard meaning (P0xxx, P2xxx, C0xxx, B0xxx, U0xxx);
    /// the others (P1xxx, P3xxx...) are defined by each manufacturer.
    /// </summary>
    public bool IsGeneric => ((Raw >> 12) & 0x3) == 0 || (Raw >> 14 == 0 && ((Raw >> 12) & 0x3) == 2);

    public static Dtc Parse(string code)
    {
        var letter = code.Length == 5 ? "PCBU".IndexOf(char.ToUpperInvariant(code[0])) : -1;
        if (letter < 0)
            throw new FormatException($"'{code}' is not a DTC such as P0301.");
        var rest = ushort.Parse(code[1..], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        if (rest > 0x3FFF)
            throw new FormatException($"'{code}' is not a DTC such as P0301.");
        return new Dtc((ushort)((letter << 14) | rest));
    }

    public override string ToString() => Code;
}
