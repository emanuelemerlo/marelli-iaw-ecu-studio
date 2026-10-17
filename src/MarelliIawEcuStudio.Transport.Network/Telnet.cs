namespace MarelliIawEcuStudio.Transport.Network;

/// <summary>Telnet (RFC 854) and COM port control (RFC 2217) constants.</summary>
internal static class Telnet
{
    public const byte Iac = 255;
    public const byte Dont = 254;
    public const byte Do = 253;
    public const byte Wont = 252;
    public const byte Will = 251;
    public const byte Sb = 250;
    public const byte Se = 240;

    public const byte OptionBinary = 0;
    public const byte OptionSuppressGoAhead = 3;
    public const byte OptionComPort = 44;

    // Client to server subcommands; the server answers with the same code + 100.
    public const byte SetBaudRate = 1;
    public const byte SetDataSize = 2;
    public const byte SetParity = 3;
    public const byte SetStopSize = 4;
    public const byte SetControl = 5;
    public const byte PurgeData = 12;
    public const byte ServerOffset = 100;

    // SET-CONTROL values.
    public const byte ControlBreakOn = 5;
    public const byte ControlBreakOff = 6;
    public const byte ControlDtrOn = 8;
    public const byte ControlDtrOff = 9;
    public const byte ControlRtsOn = 11;
    public const byte ControlRtsOff = 12;

    public static bool Supported(byte option) => option is OptionBinary or OptionSuppressGoAhead or OptionComPort;

    /// <summary>Doubles every 0xFF, as data bytes equal to IAC must be sent twice.</summary>
    public static byte[] Escape(ReadOnlySpan<byte> data)
    {
        var count = 0;
        foreach (var b in data)
            count += b == Iac ? 2 : 1;
        var result = new byte[count];
        var i = 0;
        foreach (var b in data)
        {
            result[i++] = b;
            if (b == Iac)
                result[i++] = Iac;
        }
        return result;
    }

    public static byte[] Subnegotiation(byte command, ReadOnlySpan<byte> value)
    {
        var body = new List<byte> { Iac, Sb, OptionComPort, command };
        body.AddRange(Escape(value));
        body.Add(Iac);
        body.Add(Se);
        return [.. body];
    }

    public static byte[] BigEndian(int value) => [(byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value];
}

/// <summary>
/// Splits a Telnet byte stream into data bytes, option negotiations and
/// COM port subnegotiations. Fed one byte at a time.
/// </summary>
internal sealed class TelnetParser
{
    private enum State
    {
        Data,
        Iac,
        Option,
        Sub,
        SubIac,
    }

    private readonly List<byte> _sub = [];
    private State _state;
    private byte _verb;

    /// <summary>A data byte.</summary>
    public Action<byte>? Data { get; init; }

    /// <summary>WILL/WONT/DO/DONT and the option.</summary>
    public Action<byte, byte>? Negotiation { get; init; }

    /// <summary>A subnegotiation: option, then its payload.</summary>
    public Action<byte, byte[]>? Subnegotiation { get; init; }

    public void Feed(byte b)
    {
        switch (_state)
        {
            case State.Data:
                if (b == Telnet.Iac)
                    _state = State.Iac;
                else
                    Data?.Invoke(b);
                break;
            case State.Iac:
                if (b == Telnet.Iac)
                {
                    Data?.Invoke(b);
                    _state = State.Data;
                }
                else if (b is Telnet.Will or Telnet.Wont or Telnet.Do or Telnet.Dont)
                {
                    _verb = b;
                    _state = State.Option;
                }
                else if (b == Telnet.Sb)
                {
                    _sub.Clear();
                    _state = State.Sub;
                }
                else
                {
                    _state = State.Data; // NOP, GA and other commands carry no data
                }
                break;
            case State.Option:
                Negotiation?.Invoke(_verb, b);
                _state = State.Data;
                break;
            case State.Sub:
                if (b == Telnet.Iac)
                    _state = State.SubIac;
                else
                    _sub.Add(b);
                break;
            case State.SubIac:
                if (b == Telnet.Iac)
                {
                    _sub.Add(b);
                    _state = State.Sub;
                }
                else
                {
                    // IAC SE ends the subnegotiation.
                    if (_sub.Count > 0)
                        Subnegotiation?.Invoke(_sub[0], [.. _sub.Skip(1)]);
                    _state = State.Data;
                }
                break;
        }
    }
}
