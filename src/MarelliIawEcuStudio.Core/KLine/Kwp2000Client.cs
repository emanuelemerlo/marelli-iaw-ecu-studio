namespace MarelliIawEcuStudio.Core.KLine;

/// <summary>The ECU refused a KWP2000 request (negative response 7F sid nrc).</summary>
public sealed class KwpNegativeResponseException(byte service, byte code)
    : IOException($"The ECU refused service 0x{service:X2}: 0x{code:X2} {Kwp2000Client.Describe(code)}.")
{
    public byte Service { get; } = service;

    public byte Code { get; } = code;
}

/// <summary>
/// Generic KWP2000 (ISO 14230-3) services on a <see cref="KLineLink"/>. It
/// knows the services, not the data of any particular ECU: local
/// identifiers and their meaning belong in ECU definitions.
/// </summary>
public sealed class Kwp2000Client(KLineLink link)
{
    public const byte StartDiagnosticSessionId = 0x10;
    public const byte ClearDiagnosticInformationId = 0x14;
    public const byte ReadDtcByStatusId = 0x18;
    public const byte ReadEcuIdentificationId = 0x1A;
    public const byte ReadDataByLocalIdentifierId = 0x21;
    public const byte TesterPresentId = 0x3E;
    public const byte StopCommunicationId = 0x82;

    public KLineLink Link { get; } = link;

    /// <summary>Services that only read; anything else is an active command.</summary>
    public static bool IsReadOnly(byte service) => service is
        0x1A or 0x21 or 0x22 or 0x23 or 0x17 or 0x18 or 0x12 or 0x3E or 0x81 or 0x82 or 0x10;

    /// <summary>
    /// Sends a request and returns the data of the positive answer (after the
    /// service id + 0x40) of the first ECU that answered.
    /// </summary>
    /// <exception cref="KwpNegativeResponseException">The ECU answered 7F.</exception>
    /// <exception cref="KLineException">No answer.</exception>
    public byte[] Send(byte service, params byte[] parameters)
    {
        var frames = Link.Request([service, .. parameters]);
        foreach (var f in frames)
        {
            if (f.Data.Length >= 1 && f.Data[0] == (byte)(service + 0x40))
                return f.Data[1..];
        }
        var negative = frames.FirstOrDefault(f => f.Data is [0x7F, var s, _, ..] && s == service);
        if (negative is not null)
            throw new KwpNegativeResponseException(service, negative.Data[2]);
        throw new KLineException(frames.Count == 0
            ? $"No answer to service 0x{service:X2}."
            : $"Unexpected answer to service 0x{service:X2}: {frames[0]}.");
    }

    public void StartDiagnosticSession(byte mode = 0x81) => Send(StartDiagnosticSessionId, mode);

    public void TesterPresent() => Send(TesterPresentId, 0x01);

    /// <summary>ReadEcuIdentification; option 0x80 is the whole identification table on most ECUs.</summary>
    public byte[] ReadEcuIdentification(byte option = 0x80) => Send(ReadEcuIdentificationId, option);

    public byte[] ReadDataByLocalIdentifier(byte localId) => Send(ReadDataByLocalIdentifierId, localId);

    /// <summary>ReadDiagnosticTroubleCodesByStatus: count, then 3 bytes per DTC (2 code bytes and the status).</summary>
    public IReadOnlyList<(ushort Code, byte Status)> ReadDtcByStatus(byte status = 0x02, ushort group = 0xFF00)
    {
        var data = Send(ReadDtcByStatusId, status, (byte)(group >> 8), (byte)group);
        var count = data.Length > 0 ? data[0] : 0;
        var result = new List<(ushort, byte)>();
        for (var i = 0; i < count && 1 + i * 3 + 2 < data.Length; i++)
            result.Add(((ushort)((data[1 + i * 3] << 8) | data[2 + i * 3]), data[3 + i * 3]));
        return result;
    }

    /// <summary>ClearDiagnosticInformation for all groups. An active command.</summary>
    public void ClearDiagnosticInformation(ushort group = 0xFF00) => Send(ClearDiagnosticInformationId, (byte)(group >> 8), (byte)group);

    public void StopCommunication() => Send(StopCommunicationId);

    /// <summary>Negative response codes of ISO 14230-3.</summary>
    public static string Describe(byte code) => code switch
    {
        0x10 => "general reject",
        0x11 => "service not supported",
        0x12 => "sub-function not supported or invalid format",
        0x21 => "busy, repeat request",
        0x22 => "conditions not correct or request sequence error",
        0x23 => "routine not complete",
        0x31 => "request out of range",
        0x33 => "security access denied",
        0x35 => "invalid key",
        0x36 => "exceeded number of attempts",
        0x37 => "required time delay not expired",
        0x78 => "response pending",
        0x80 => "service not supported in active diagnostic session",
        _ => "unknown code",
    };
}
