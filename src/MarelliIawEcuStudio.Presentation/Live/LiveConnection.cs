using System.Diagnostics;
using MarelliIawEcuStudio.Core.Decoding;
using MarelliIawEcuStudio.Core.Session;

namespace MarelliIawEcuStudio.Presentation.Live;

/// <summary>One completed polling cycle, decoded on the polling thread.</summary>
/// <param name="TimeSeconds">Seconds since polling started.</param>
/// <param name="Values">Decoded value per parameter id; null when the read failed.</param>
public sealed record LiveCycle(double TimeSeconds, IReadOnlyDictionary<string, decimal?> Values, IReadOnlyList<ErrorState>? Errors);

/// <summary>
/// Runs the polling loop of a connected <see cref="DiagnosticSession"/> on a
/// background thread (IES2 used a BackgroundWorker on the form). Active
/// commands can run while it polls: the session serialises line access.
/// </summary>
public sealed class LiveConnection : IAsyncDisposable
{
    private readonly Stopwatch _clock = new();
    private volatile ParameterDecoder[] _selected = [];
    private CancellationTokenSource? _stop;
    private Task? _loop;

    public LiveConnection(DiagnosticSession session)
    {
        Session = session;
    }

    public DiagnosticSession Session { get; }

    public RateMeter Rate { get; } = new();

    /// <summary>Read engine and immobiliser errors every few cycles.</summary>
    public bool ReadErrors { get; set; } = true;

    public bool IsRunning => _loop is { IsCompleted: false };

    public TimeSpan Elapsed => _clock.Elapsed;

    /// <summary>
    /// Raised on the polling thread after every cycle, with the register
    /// buffer still consistent. Handlers must be quick (copy, then post to the UI).
    /// </summary>
    public event Action<LiveCycle, RegisterBuffer>? CycleCompleted;

    /// <summary>Raised on the polling thread when the loop stops because of an error.</summary>
    public event Action<Exception>? Faulted;

    public void SetSelection(IEnumerable<ParameterDecoder> parameters) => _selected = [.. parameters];

    public void Start()
    {
        if (IsRunning)
            return;
        _stop = new CancellationTokenSource();
        var token = _stop.Token;
        _clock.Restart();
        Rate.Reset();
        _loop = Task.Factory.StartNew(() => Run(token), token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
    }

    private void Run(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                var selected = _selected;
                var errorReads = 0;
                void OnErrors(DiagnosticSession _) => errorReads++;
                Session.ErrorsUpdated += OnErrors;
                try
                {
                    Session.PollOnce(new PollRequest { Parameters = selected, Errors = ReadErrors });
                }
                finally
                {
                    Session.ErrorsUpdated -= OnErrors;
                }

                var values = new Dictionary<string, decimal?>(selected.Length, StringComparer.Ordinal);
                foreach (var p in selected)
                    values[p.Id] = p.IsValid(Session.Buffer) ? p.Decode(Session.Buffer) : null;
                IReadOnlyList<ErrorState>? errors = null;
                if (errorReads > 0)
                {
                    errors = [.. Session.EngineErrors().Concat(Session.ImmobilizerErrors())];
                }
                var now = _clock.Elapsed;
                if (selected.Length > 0)
                    Rate.Tick(now);
                CycleCompleted?.Invoke(new LiveCycle(now.TotalSeconds, values, errors), Session.Buffer);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Faulted?.Invoke(ex);
        }
    }

    public async Task StopAsync()
    {
        if (_stop is null || _loop is null)
            return;
        await _stop.CancelAsync();
        try
        {
            await _loop;
        }
        catch (OperationCanceledException)
        {
        }
        _stop.Dispose();
        _stop = null;
        _loop = null;
    }

    /// <summary>Runs a session command on a background thread.</summary>
    public Task<T> RunAsync<T>(Func<DiagnosticSession, T> command) => Task.Run(() => command(Session));

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        Session.Dispose();
    }
}
