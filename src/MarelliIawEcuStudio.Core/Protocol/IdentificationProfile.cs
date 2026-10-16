namespace MarelliIawEcuStudio.Core.Protocol;

/// <summary>
/// How an ECU family enters diagnostic mode and where it exposes its
/// identification codes.
/// </summary>
/// <param name="Name">Profile name used in definition files.</param>
/// <param name="SlowInit">Whether the 1200 baud init sequence 0x0F 0xAA 0xCC is needed.</param>
/// <param name="IsoRequests">Requests returning the 6 ISO code bytes (the first is the 0x55 sync).</param>
/// <param name="CodricRequests">Requests returning the 10 ASCII digits of the CODRIC spare part code.</param>
/// <param name="IsoFallback">
/// ISO code assumed when the first ISO byte is not the 0x55 sync. IES2 used
/// <c>55D085??????</c> because early IAW-16F units answer that way.
/// </param>
public sealed record IdentificationProfile(string Name, bool SlowInit, byte[] IsoRequests, byte[] CodricRequests, string? IsoFallback)
{
    public const byte SyncByte = 0x55;

    public static IdentificationProfile Standard { get; } = new(
        "standard", true,
        [0x2A, 0x2B, 0x2C, 0x2D, 0x2E, 0x2F],
        [0x17, 0x18, 0x19, 0x1A, 0x1B, 0x1C, 0x1D, 0x1E, 0x1F, 0x20],
        "55D085??????");

    /// <summary>Weber-Marelli IAW-04K.P8.</summary>
    public static IdentificationProfile P8 { get; } = new(
        "p8", false,
        [0x30, 0x31, 0x32, 0x33, 0x34, 0x35],
        [0x36, 0x37, 0x38, 0x39, 0x3A, 0x3B, 0x3C, 0x3D, 0x3E, 0x3F],
        null);

    public static bool TryGet(string name, out IdentificationProfile profile)
    {
        profile = name switch
        {
            "standard" => Standard,
            "p8" => P8,
            _ => null!,
        };
        return profile is not null;
    }

    public static IdentificationProfile Get(string name) =>
        TryGet(name, out var p) ? p : throw new ArgumentException($"unknown identification profile '{name}'", nameof(name));
}
