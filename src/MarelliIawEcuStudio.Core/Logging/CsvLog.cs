using System.Globalization;
using System.Text;
using MarelliIawEcuStudio.Core.Decoding;
using MarelliIawEcuStudio.Core.Session;

namespace MarelliIawEcuStudio.Core.Logging;

/// <summary>A column of a data log.</summary>
public sealed record LogColumn(string Id, string Name, string Unit);

/// <summary>A data log loaded in memory: metadata, columns and samples.</summary>
public sealed class DataLog
{
    public IReadOnlyDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>();

    public required IReadOnlyList<LogColumn> Columns { get; init; }

    /// <summary>Samples ordered by time; <c>Values[i]</c> belongs to <c>Columns[i]</c> and is NaN when missing.</summary>
    public required IReadOnlyList<LogSample> Samples { get; init; }

    public bool IsLegacyIes2 { get; init; }
}

public sealed record LogSample(long TimeMs, double[] Values);

/// <summary>
/// Writes live data as CSV: <c>;</c> separated as in IES2, invariant culture
/// numbers, and a <c>#</c> metadata header that identifies the ECU so a log
/// can be replayed or re-decoded later.
/// </summary>
/// <remarks>
/// Layout:
/// <code>
/// # marelli-iaw-ecu-studio log v1
/// # ecu=IAW-6F/16F
/// # iso=55D085029440
/// # columns=PERIODE;MT_EAU_L
/// # units=rpm;°C
/// Time [ms];Engine speed [rpm];Coolant temperature [°C]
/// 0;850;88
/// </code>
/// </remarks>
public sealed class CsvLogWriter : IDisposable
{
    public const string Signature = "# marelli-iaw-ecu-studio log v1";
    private const char Separator = ';';

    private readonly TextWriter _writer;
    private readonly IReadOnlyList<ParameterDecoder> _columns;
    private readonly bool _ownsWriter;

    public CsvLogWriter(TextWriter writer, ConnectionInfo connection, IReadOnlyList<ParameterDecoder> columns, string appVersion, bool ownsWriter = false)
    {
        _writer = writer;
        _columns = columns;
        _ownsWriter = ownsWriter;
        _writer.WriteLine(Signature);
        WriteMeta("app", appVersion);
        WriteMeta("ecu", connection.Definition.Id);
        WriteMeta("iso", connection.Iso);
        WriteMeta("codric", connection.Codric);
        WriteMeta("vehicle", connection.VehicleModel);
        WriteMeta("started", DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture));
        WriteMeta("columns", string.Join(Separator, columns.Select(c => c.Id)));
        WriteMeta("units", string.Join(Separator, columns.Select(c => c.GetUnit(CultureInfo.InvariantCulture))));
        var header = new StringBuilder("Time [ms]");
        foreach (var c in columns)
        {
            header.Append(Separator).Append(Clean(c.GetName()));
            var unit = c.GetUnit();
            if (unit.Length > 0)
                header.Append(" [").Append(Clean(unit)).Append(']');
        }
        _writer.WriteLine(header);
    }

    public static CsvLogWriter Create(string path, ConnectionInfo connection, IReadOnlyList<ParameterDecoder> columns, string appVersion)
    {
        var dir = Path.GetDirectoryName(Path.GetFullPath(path));
        if (dir is not null)
            Directory.CreateDirectory(dir);
        return new CsvLogWriter(new StreamWriter(path, append: false, new UTF8Encoding(false)), connection, columns, appVersion, ownsWriter: true);
    }

    /// <summary>Appends one row with the current decoded values; invalid values are left empty.</summary>
    public void WriteSample(long timeMs, RegisterBuffer buffer)
    {
        var row = new StringBuilder(timeMs.ToString(CultureInfo.InvariantCulture));
        foreach (var c in _columns)
        {
            row.Append(Separator);
            if (c.IsValid(buffer) && c.Decode(buffer) is { } value)
                row.Append(value.ToString(CultureInfo.InvariantCulture));
        }
        _writer.WriteLine(row);
    }

    public void Flush() => _writer.Flush();

    private void WriteMeta(string key, string? value)
    {
        if (value is not null)
            _writer.WriteLine($"# {key}={Clean(value)}");
    }

    private static string Clean(string s) => s.Replace('\r', ' ').Replace('\n', ' ');

    public void Dispose()
    {
        _writer.Flush();
        if (_ownsWriter)
            _writer.Dispose();
    }
}

/// <summary>Reads logs written by <see cref="CsvLogWriter"/> and the <c>IESexp_*.csv</c> files of IES2.</summary>
public static class CsvLogReader
{
    public static DataLog Read(string path)
    {
        using var reader = new StreamReader(path, detectEncodingFromByteOrderMarks: true);
        return Read(reader);
    }

    public static DataLog Read(TextReader reader)
    {
        var metadata = new Dictionary<string, string>(StringComparer.Ordinal);
        string? header = null;
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (line.StartsWith('#'))
            {
                var eq = line.IndexOf('=');
                if (eq > 1)
                    metadata[line[1..eq].Trim()] = line[(eq + 1)..].Trim();
                continue;
            }
            if (line.Length > 0)
            {
                header = line;
                break;
            }
        }
        if (header is null)
            throw new InvalidDataException("The log has no header line.");

        var names = header.Split(';');
        var legacy = !metadata.ContainsKey("columns") && names[0] == "Timestamp";
        var ids = metadata.TryGetValue("columns", out var c) ? c.Split(';') : names[1..];
        var units = metadata.TryGetValue("units", out var u) ? u.Split(';') : new string[ids.Length];
        var columns = new List<LogColumn>();
        for (var i = 1; i < names.Length; i++)
            columns.Add(new LogColumn(i - 1 < ids.Length ? ids[i - 1] : names[i], StripUnit(names[i]), i - 1 < units.Length ? units[i - 1] ?? "" : ""));

        var samples = new List<LogSample>();
        while ((line = reader.ReadLine()) is not null)
        {
            if (line.Length == 0 || line.StartsWith('#'))
                continue;
            var cells = line.Split(';');
            if (!long.TryParse(cells[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var time))
                continue;
            var values = new double[columns.Count];
            for (var i = 0; i < values.Length; i++)
                values[i] = i + 1 < cells.Length ? ParseNumber(cells[i + 1]) : double.NaN;
            samples.Add(new LogSample(time, values));
        }
        return new DataLog { Metadata = metadata, Columns = columns, Samples = samples, IsLegacyIes2 = legacy };
    }

    private static string StripUnit(string name)
    {
        var i = name.LastIndexOf(" [", StringComparison.Ordinal);
        return i > 0 && name.EndsWith(']') ? name[..i] : name;
    }

    /// <summary>
    /// IES2 wrote numbers in the PC's culture, so "12,5" (Italian, Polish...)
    /// and "12.5" are both accepted.
    /// </summary>
    private static double ParseNumber(string cell)
    {
        if (cell.Length == 0)
            return double.NaN;
        if (double.TryParse(cell, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
            return v;
        return double.TryParse(cell.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out v) ? v : double.NaN;
    }
}
