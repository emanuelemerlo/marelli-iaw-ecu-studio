using System.Diagnostics;
using System.Globalization;
using System.Text;
using MarelliIawEcuStudio.Core.Protocol;

namespace MarelliIawEcuStudio.Core.Logging;

/// <summary>One event of a line trace.</summary>
/// <param name="TimeMs">Milliseconds since the trace started.</param>
/// <param name="Value">The byte; for <see cref="LineDirection.Timeout"/> the request that got no answer.</param>
public sealed record TraceEvent(double TimeMs, LineDirection Direction, int BaudRate, byte Value, string? Note = null);

/// <summary>A line trace loaded in memory.</summary>
public sealed class LineTrace
{
    public IReadOnlyDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>();

    public required IReadOnlyList<TraceEvent> Events { get; init; }

    /// <summary>Requests sent at the communication speed (polling, identification, active mode).</summary>
    public int RequestCount => Events.Count(e => e.Direction == LineDirection.Sent && e.BaudRate == IawLink.CommunicationBaudRate);

    public int TimeoutCount => Events.Count(e => e.Direction == LineDirection.Timeout);
}

/// <summary>
/// Writes every byte seen on the K-Line to a text file, one event per line,
/// so a session on a real car can be examined or replayed later
/// (see <c>TraceReplayTransport</c> in the simulator).
/// </summary>
/// <remarks>
/// Layout (<c>;</c> separated, invariant culture, bytes in hex):
/// <code>
/// # marelli-iaw-ecu-studio trace v1
/// # transport=/dev/ttyUSB0
/// # started=2026-10-17T07:40:00.0000000+00:00
/// Time [ms];Event;Baud;Byte;Note
/// 0.000;MARK;9600;;connect ecu=auto
/// 0.412;TX;1200;0F;
/// 410.102;DISC;1200;0F;
/// 452.880;TX;7680;01;
/// 453.920;ECHO;7680;01;
/// 455.010;RX;7680;2C;
/// 762.300;TO;7680;02;
/// </code>
/// Events: TX sent, ECHO adapter echo, RX from the ECU, TO no answer to the
/// request in the byte column, DISC read and discarded, MARK a note.
/// Lines starting with <c>#</c> after the header are comments.
/// </remarks>
public sealed class LineTraceWriter : IDisposable
{
    public const string Signature = "# marelli-iaw-ecu-studio trace v1";
    public const string Header = "Time [ms];Event;Baud;Byte;Note";
    private const char Separator = ';';

    private readonly Lock _gate = new();
    private readonly TextWriter _writer;
    private readonly bool _ownsWriter;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private bool _disposed;

    public LineTraceWriter(TextWriter writer, IEnumerable<KeyValuePair<string, string?>>? metadata = null, bool ownsWriter = false)
    {
        _writer = writer;
        _ownsWriter = ownsWriter;
        _writer.WriteLine(Signature);
        _writer.WriteLine($"# started={DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture)}");
        foreach (var (key, value) in metadata ?? [])
        {
            if (value is not null)
                _writer.WriteLine($"# {key}={Clean(value)}");
        }
        _writer.WriteLine(Header);
        _writer.Flush();
    }

    /// <summary>Creates the file (and its folder). The stream is flushed after each event, so a crash loses nothing.</summary>
    public static LineTraceWriter Create(string path, IEnumerable<KeyValuePair<string, string?>>? metadata = null)
    {
        var dir = Path.GetDirectoryName(Path.GetFullPath(path));
        if (dir is not null)
            Directory.CreateDirectory(dir);
        var stream = new StreamWriter(path, false, new UTF8Encoding(false)) { AutoFlush = true };
        return new LineTraceWriter(stream, metadata, ownsWriter: true);
    }

    /// <summary>Number of events written.</summary>
    public long EventCount { get; private set; }

    /// <summary>Records one event; pass this method to <see cref="IawLink.Monitor"/>.</summary>
    public void Write(LineEvent e)
    {
        var time = _clock.Elapsed.TotalMilliseconds;
        lock (_gate)
        {
            if (_disposed)
                return;
            var sb = new StringBuilder(40);
            sb.Append(time.ToString("0.000", CultureInfo.InvariantCulture)).Append(Separator)
              .Append(Code(e.Direction)).Append(Separator)
              .Append(e.BaudRate.ToString(CultureInfo.InvariantCulture)).Append(Separator);
            if (e.Direction != LineDirection.Marker)
                sb.Append(e.Value.ToString("X2", CultureInfo.InvariantCulture));
            sb.Append(Separator);
            if (e.Note is not null)
                sb.Append(Clean(e.Note));
            _writer.WriteLine(sb);
            EventCount++;
        }
    }

    /// <summary>Adds a comment line, for example the line statistics at the end.</summary>
    public void Comment(string text)
    {
        lock (_gate)
        {
            if (!_disposed)
                _writer.WriteLine("# " + Clean(text));
        }
    }

    internal static string Code(LineDirection d) => d switch
    {
        LineDirection.Sent => "TX",
        LineDirection.Received => "RX",
        LineDirection.Echo => "ECHO",
        LineDirection.Timeout => "TO",
        LineDirection.Discarded => "DISC",
        LineDirection.Marker => "MARK",
        _ => throw new ArgumentOutOfRangeException(nameof(d)),
    };

    private static string Clean(string s) => s.Replace(Separator, ',').Replace('\r', ' ').Replace('\n', ' ');

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
                return;
            _disposed = true;
            _writer.Flush();
            if (_ownsWriter)
                _writer.Dispose();
        }
    }
}

/// <summary>Reads files written by <see cref="LineTraceWriter"/>.</summary>
public static class LineTraceReader
{
    public static LineTrace Read(string path)
    {
        using var reader = new StreamReader(path, Encoding.UTF8);
        return Read(reader, path);
    }

    public static LineTrace Read(TextReader reader, string? origin = null)
    {
        var where = origin ?? "trace";
        var first = reader.ReadLine();
        if (first?.Trim() != LineTraceWriter.Signature)
            throw new InvalidDataException($"{where}: not a line trace (missing '{LineTraceWriter.Signature}').");
        var metadata = new Dictionary<string, string>(StringComparer.Ordinal);
        var events = new List<TraceEvent>();
        var lineNumber = 1;
        var headerSeen = false;
        while (reader.ReadLine() is { } line)
        {
            lineNumber++;
            if (line.Length == 0)
                continue;
            if (line.StartsWith('#'))
            {
                var eq = line.IndexOf('=');
                if (!headerSeen && eq > 2)
                    metadata[line[2..eq].Trim()] = line[(eq + 1)..];
                continue;
            }
            if (!headerSeen)
            {
                headerSeen = true;
                if (line.StartsWith("Time", StringComparison.Ordinal))
                    continue;
            }
            events.Add(ParseEvent(line, where, lineNumber));
        }
        return new LineTrace { Metadata = metadata, Events = events };
    }

    private static TraceEvent ParseEvent(string line, string where, int lineNumber)
    {
        var f = line.Split(';', 5);
        if (f.Length < 4)
            throw new InvalidDataException($"{where}:{lineNumber}: expected 'time;event;baud;byte;note'.");
        if (!double.TryParse(f[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var time)
            || !int.TryParse(f[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var baud))
            throw new InvalidDataException($"{where}:{lineNumber}: bad time or baud rate.");
        var direction = f[1] switch
        {
            "TX" => LineDirection.Sent,
            "RX" => LineDirection.Received,
            "ECHO" => LineDirection.Echo,
            "TO" => LineDirection.Timeout,
            "DISC" => LineDirection.Discarded,
            "MARK" => LineDirection.Marker,
            _ => throw new InvalidDataException($"{where}:{lineNumber}: unknown event '{f[1]}'."),
        };
        byte value = 0;
        if (direction != LineDirection.Marker
            && !byte.TryParse(f[3], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value))
            throw new InvalidDataException($"{where}:{lineNumber}: bad byte '{f[3]}'.");
        var note = f.Length > 4 && f[4].Length > 0 ? f[4] : null;
        return new TraceEvent(time, direction, baud, value, note);
    }
}
