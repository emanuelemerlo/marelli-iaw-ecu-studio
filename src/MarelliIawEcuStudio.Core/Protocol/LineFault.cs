namespace MarelliIawEcuStudio.Core.Protocol;

/// <summary>What went wrong on the K-Line during one request.</summary>
public enum LineFault
{
    /// <summary>Echo (when expected) and answer arrived as they should.</summary>
    None,

    /// <summary>
    /// Nothing came back, not even the echo of the sent byte: the cable, the
    /// USB port or the adapter is not working, or the adapter does not echo.
    /// </summary>
    NoEcho,

    /// <summary>
    /// The first byte back was not the sent byte. Noise on the line, a wrong
    /// line speed, or an adapter that does not echo (its first byte is then
    /// the ECU answer).
    /// </summary>
    EchoMismatch,

    /// <summary>The echo arrived but the ECU did not answer: ignition, connector or K-Line wire.</summary>
    NoAnswer,
}

/// <summary>Result of one request on the line.</summary>
/// <param name="Answered">An answer byte was read. True also with <see cref="LineFault.EchoMismatch"/>, as IES2 accepted it.</param>
/// <param name="Response">The answer byte, 0 when not answered.</param>
/// <param name="Fault">The first problem seen, or <see cref="LineFault.None"/>.</param>
/// <param name="Echo">The echo byte read, when the adapter echoes and one arrived.</param>
public readonly record struct QueryResult(bool Answered, byte Response, LineFault Fault, byte? Echo);

/// <summary>Counters of the requests sent on a line, by outcome.</summary>
public sealed class LineStatistics
{
    private long _requests;
    private long _answered;
    private long _noEcho;
    private long _echoMismatch;
    private long _noAnswer;

    public long Requests => Interlocked.Read(ref _requests);

    public long Answered => Interlocked.Read(ref _answered);

    public long NoEcho => Interlocked.Read(ref _noEcho);

    public long EchoMismatch => Interlocked.Read(ref _echoMismatch);

    public long NoAnswer => Interlocked.Read(ref _noAnswer);

    public LineFault LastFault { get; private set; }

    internal void Record(QueryResult r)
    {
        Interlocked.Increment(ref _requests);
        if (r.Answered)
            Interlocked.Increment(ref _answered);
        switch (r.Fault)
        {
            case LineFault.NoEcho:
                Interlocked.Increment(ref _noEcho);
                break;
            case LineFault.EchoMismatch:
                Interlocked.Increment(ref _echoMismatch);
                break;
            case LineFault.NoAnswer:
                Interlocked.Increment(ref _noAnswer);
                break;
        }
        if (r.Fault != LineFault.None)
            LastFault = r.Fault;
    }

    public override string ToString() =>
        $"{Requests} requests, {Answered} answered, {NoAnswer} without answer, {NoEcho} without echo, {EchoMismatch} with wrong echo";
}

public static class LineFaultText
{
    /// <summary>A short English explanation with the most likely cause, for logs and the command line.</summary>
    public static string Describe(LineFault fault) => fault switch
    {
        LineFault.NoEcho => "nothing came back from the adapter, not even the echo: check the cable and the USB port, or use --no-echo if the adapter does not echo",
        LineFault.EchoMismatch => "the adapter echoed a different byte: noise on the line, a wrong speed, or an adapter that does not echo (try --no-echo or --compat)",
        LineFault.NoAnswer => "the adapter echoes but the ECU does not answer: check the ignition, the 3-pin connector and the K-Line wire",
        _ => "no fault",
    };
}
