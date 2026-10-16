namespace MarelliIawEcuStudio.Core.Decoding;

/// <summary>
/// The last value received for each of the 256 single-byte requests, with a
/// flag telling whether the last query succeeded. Mirrors the
/// <c>Buffer</c>/<c>Valid</c> arrays of IES2.
/// </summary>
public sealed class RegisterBuffer
{
    private readonly byte[] _values = new byte[256];
    private readonly bool[] _valid = new bool[256];

    public byte this[byte register]
    {
        get => _values[register];
        set => _values[register] = value;
    }

    public bool IsValid(byte register) => _valid[register];

    public void Store(byte register, byte value, bool valid)
    {
        _values[register] = value;
        _valid[register] = valid;
    }

    public void Clear()
    {
        Array.Clear(_values);
        Array.Clear(_valid);
    }

    public bool GetBit(byte register, int bit) => (_values[register] & (1 << bit)) != 0;

    /// <summary>Copy of the current values, for logging raw data.</summary>
    public byte[] Snapshot() => (byte[])_values.Clone();
}
