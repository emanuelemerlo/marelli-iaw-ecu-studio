namespace MarelliIawEcuStudio.Core.Protocol;

/// <summary>
/// Delays and timeouts of the IAW protocol. <see cref="Default"/> holds the
/// values used by IES2; <see cref="Compatibility"/> its "compatibility mode"
/// for slow adapters. Tests use <see cref="Fast"/> with the simulator.
/// </summary>
public sealed record ProtocolTiming
{
    /// <summary>Delays after each of the three slow-init bytes (0x0F, 0xAA, 0xCC).</summary>
    public int[] SlowInitDelaysMs { get; init; } = [110, 110, 150];

    /// <summary>
    /// Longest wait for the adapter echoes of the three slow-init bytes. They
    /// arrive at once on a cable; a network bridge adds its latency.
    /// </summary>
    public int InitEchoWaitMs { get; init; } = 500;

    /// <summary>Pause before each identification request.</summary>
    public int IdentificationDelayMs { get; init; } = 5;

    /// <summary>Pause after each request in the polling loop (IES2: 4 ms, 20 ms in compatibility mode).</summary>
    public int PollingDelayMs { get; init; } = 4;

    /// <summary>Pause when there is nothing to poll.</summary>
    public int IdleDelayMs { get; init; } = 20;

    /// <summary>How long to listen for the ISO code broadcast by the ECU at key-on.</summary>
    public int IsoBroadcastWaitMs { get; init; } = 10_000;

    /// <summary>Pause after receiving the broadcast ISO code.</summary>
    public int IsoBroadcastSettleMs { get; init; } = 550;

    /// <summary>Polling interval while waiting for the broadcast.</summary>
    public int IsoBroadcastPollMs { get; init; } = 15;

    public int IsoBroadcastReadTimeoutMs { get; init; } = 1200;

    public int PollingReadTimeoutMs { get; init; } = 300;

    public int ActiveTestReadTimeoutMs { get; init; } = 500;

    public int AdjustmentReadTimeoutMs { get; init; } = 300;

    /// <summary>Delays around each code sent in active mode.</summary>
    public int ActiveCodeDelayBeforeMs { get; init; } = 5;

    public int ActiveCodeDelayAfterMs { get; init; } = 10;

    /// <summary>Polling interval while waiting for a test result.</summary>
    public int ActiveResultPollMs { get; init; } = 15;

    /// <summary>Extra time added to a test's own timeout before giving up.</summary>
    public int ActiveResultGraceMs { get; init; } = 1000;

    /// <summary>
    /// Waits before each attempt to restore a lost connection; the last one
    /// repeats until the ECU answers again.
    /// </summary>
    public int[] ReconnectDelaysMs { get; init; } = [500, 1000, 2000, 5000];

    public static ProtocolTiming Default { get; } = new();

    public static ProtocolTiming Compatibility { get; } = new() { PollingDelayMs = 20 };

    /// <summary>No waiting at all; only meaningful with the simulator.</summary>
    public static ProtocolTiming Fast { get; } = new()
    {
        SlowInitDelaysMs = [0, 0, 0],
        InitEchoWaitMs = 200,
        IdentificationDelayMs = 0,
        PollingDelayMs = 0,
        IdleDelayMs = 1,
        IsoBroadcastWaitMs = 200,
        IsoBroadcastSettleMs = 0,
        IsoBroadcastPollMs = 1,
        IsoBroadcastReadTimeoutMs = 100,
        PollingReadTimeoutMs = 50,
        ActiveTestReadTimeoutMs = 50,
        AdjustmentReadTimeoutMs = 50,
        ActiveCodeDelayBeforeMs = 0,
        ActiveCodeDelayAfterMs = 0,
        ActiveResultPollMs = 1,
        ActiveResultGraceMs = 0,
        ReconnectDelaysMs = [1],
    };
}
