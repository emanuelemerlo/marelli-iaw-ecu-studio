namespace MarelliIawEcuStudio.Core.KLine;

/// <summary>Framed K-Line protocols (the IAW single-byte protocol is not one of them).</summary>
public enum KLineProtocol
{
    /// <summary>ISO 9141-2: 5-baud init, keywords 08 08 or 94 94, header 68 6A F1.</summary>
    Iso9141,

    /// <summary>ISO 14230 (KWP2000) with the 5-baud init, keyword 2 = 0x8F.</summary>
    Kwp2000Slow,

    /// <summary>ISO 14230 (KWP2000) with the 25 ms fast init and StartCommunication.</summary>
    Kwp2000Fast,
}

/// <summary>One message on a framed K-Line: addresses and service data, without header and checksum.</summary>
public sealed record KLineFrame(byte Target, byte Source, byte[] Data)
{
    public override string ToString() => $"{Source:X2}→{Target:X2}: {Convert.ToHexString(Data)}";
}

/// <summary>Builds and parses ISO 9141-2 and ISO 14230 messages.</summary>
public static class KLineFraming
{
    public const byte TesterAddress = 0xF1;

    /// <summary>Functional address of the OBD emission-related ECUs.</summary>
    public const byte ObdFunctionalAddress = 0x33;

    public static byte Checksum(ReadOnlySpan<byte> bytes)
    {
        var sum = 0;
        foreach (var b in bytes)
            sum += b;
        return (byte)sum;
    }

    /// <summary>A request from the tester, header and checksum included.</summary>
    public static byte[] EncodeRequest(KLineProtocol protocol, byte target, ReadOnlySpan<byte> data)
    {
        if (data.Length == 0)
            throw new ArgumentException("A message needs at least the service id.", nameof(data));
        List<byte> message;
        if (protocol == KLineProtocol.Iso9141)
        {
            if (data.Length > 7)
                throw new ArgumentException("ISO 9141-2 messages carry at most 7 data bytes.", nameof(data));
            // 68 6A F1: functional request to the OBD ECUs.
            message = [0x68, 0x6A, TesterAddress, .. data];
        }
        else
        {
            message = data.Length <= 63
                ? [(byte)(0xC0 | data.Length), target, TesterAddress, .. data]
                : [0xC0, target, TesterAddress, (byte)data.Length, .. data];
        }
        message.Add(Checksum([.. message]));
        return [.. message];
    }

    /// <summary>A response from an ECU, header and checksum included (used by the simulator).</summary>
    public static byte[] EncodeResponse(KLineProtocol protocol, byte source, ReadOnlySpan<byte> data)
    {
        List<byte> message = protocol == KLineProtocol.Iso9141
            ? [0x48, 0x6B, source, .. data]
            : data.Length <= 63
                ? [(byte)(0x80 | data.Length), TesterAddress, source, .. data]
                : [0x80, TesterAddress, source, (byte)data.Length, .. data];
        message.Add(Checksum([.. message]));
        return [.. message];
    }

    /// <summary>
    /// Splits received bytes into messages. ISO 9141-2 has no length field, so
    /// a message ends where the checksum matches and the next header starts;
    /// ISO 14230 messages carry their length. Bytes that do not form a valid
    /// message are left out of <paramref name="consumed"/>.
    /// </summary>
    public static List<KLineFrame> Decode(KLineProtocol protocol, IReadOnlyList<byte> bytes, out int consumed)
    {
        var frames = new List<KLineFrame>();
        var i = 0;
        while (i < bytes.Count)
        {
            var length = protocol == KLineProtocol.Iso9141 ? Iso9141Length(bytes, i) : Kwp2000Length(bytes, i);
            if (length <= 0)
                break;
            var message = bytes.Skip(i).Take(length).ToArray();
            frames.Add(protocol == KLineProtocol.Iso9141
                ? new KLineFrame(message[1], message[2], message[3..^1])
                : ParseKwp(message));
            i += length;
        }
        consumed = i;
        return frames;
    }

    /// <summary>Length of the ISO 9141-2 message at <paramref name="start"/>, or 0 when incomplete.</summary>
    private static int Iso9141Length(IReadOnlyList<byte> bytes, int start)
    {
        if (bytes.Count - start < 5 || bytes[start] != 0x48)
            return 0;
        var sum = bytes[start] + bytes[start + 1] + bytes[start + 2];
        for (var end = start + 3; end < bytes.Count - 1 && end - start <= 10; end++)
        {
            sum += bytes[end];
            var checksumAt = end + 1;
            if ((byte)sum == bytes[checksumAt] && (checksumAt + 1 == bytes.Count || bytes[checksumAt + 1] == 0x48))
                return checksumAt - start + 1;
        }
        return 0;
    }

    /// <summary>Length of the ISO 14230 message at <paramref name="start"/>, or 0 when incomplete or invalid.</summary>
    private static int Kwp2000Length(IReadOnlyList<byte> bytes, int start)
    {
        if (bytes.Count - start < 2)
            return 0;
        var format = bytes[start];
        var hasAddresses = (format & 0xC0) != 0;
        var header = hasAddresses ? 3 : 1;
        var dataLength = format & 0x3F;
        if (dataLength == 0)
        {
            if (bytes.Count - start <= header)
                return 0;
            dataLength = bytes[start + header];
            header++;
        }
        var total = header + dataLength + 1;
        if (bytes.Count - start < total)
            return 0;
        var checksum = Checksum(bytes.Skip(start).Take(total - 1).ToArray());
        return checksum == bytes[start + total - 1] ? total : -1;
    }

    private static KLineFrame ParseKwp(byte[] m)
    {
        var hasAddresses = (m[0] & 0xC0) != 0;
        var header = hasAddresses ? 3 : 1;
        if ((m[0] & 0x3F) == 0)
            header++;
        return new KLineFrame(hasAddresses ? m[1] : (byte)0, hasAddresses ? m[2] : (byte)0, m[header..^1]);
    }

    /// <summary>
    /// Parses a request written by a tester (used by the simulator); returns
    /// the length of the message, 0 when more bytes are needed, -1 when invalid.
    /// </summary>
    public static int TryDecodeRequest(KLineProtocol protocol, IReadOnlyList<byte> bytes, out KLineFrame? frame)
    {
        frame = null;
        if (protocol == KLineProtocol.Iso9141)
        {
            // 68 6A F1 data... cs: no length field; complete when the checksum matches
            // and a reasonable data length is reached (the tester sends one message at a time).
            if (bytes.Count < 5)
                return 0;
            if (bytes[0] != 0x68)
                return -1;
            for (var end = 4; end < bytes.Count; end++)
            {
                if (Checksum(bytes.Take(end).ToArray()) == bytes[end] && end == bytes.Count - 1)
                {
                    frame = new KLineFrame(bytes[1], bytes[2], [.. bytes.Skip(3).Take(end - 3)]);
                    return end + 1;
                }
            }
            return bytes.Count > 11 ? -1 : 0;
        }
        var length = Kwp2000Length(bytes, 0);
        if (length > 0)
        {
            var m = bytes.Take(length).ToArray();
            var hasAddresses = (m[0] & 0xC0) != 0;
            var header = (hasAddresses ? 3 : 1) + ((m[0] & 0x3F) == 0 ? 1 : 0);
            frame = new KLineFrame(hasAddresses ? m[1] : (byte)0, hasAddresses ? m[2] : (byte)0, m[header..^1]);
        }
        return length;
    }
}
