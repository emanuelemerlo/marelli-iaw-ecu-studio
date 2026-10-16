using System.Globalization;
using System.IO.Ports;
using System.Reflection;
using IES_2.ECU;
using MarelliIawEcuStudio.Core.Decoding;
using MarelliIawEcuStudio.Core.Definitions;
using MarelliIawEcuStudio.Core.Localization;
using LegacyEcu = IES_2.ECU.ecu;

namespace MarelliIawEcuStudio.Tests;

[CollectionDefinition(nameof(LegacyCollection), DisableParallelization = true)]
public class LegacyCollection;

/// <summary>
/// Compares the JSON definitions and the new decoders with the original IES2
/// ECU modules, compiled unchanged from legacy/IES2. The IES2 classes keep
/// their state in static fields, so these tests never run in parallel.
/// </summary>
[Collection(nameof(LegacyCollection))]
public class LegacyEquivalenceTests
{
    private static readonly DefinitionRegistry Registry = DefinitionRegistry.BuiltIn();

    /// <summary>Definition id → IES2 class.</summary>
    public static readonly TheoryData<string, Type> Families = new()
    {
        { "IAW-6F/16F", typeof(iaw16f) },
        { "IAW-8F/18F", typeof(iaw18f) },
        { "IAW-8F.68", typeof(iaw8f_68) },
        { "IAW-18FD/1G7", typeof(iaw18fd) },
        { "IAW-04K.P8", typeof(iaw04k) },
        { "FIAT CODE", typeof(code) },
    };

    public LegacyEquivalenceTests()
    {
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
    }

    private static LegacyEcu CreateLegacy(Type type)
    {
        var port = new SerialPort();
        return (LegacyEcu)Activator.CreateInstance(type, BindingFlags.Public | BindingFlags.Instance, null, [port], null)!;
    }

    private static string LegacyConst(Type type, string name) => (string)type.GetField(name)!.GetRawConstantValue()!;

    [Fact]
    public void EveryLegacyModuleHasADefinition() =>
        Assert.Equal(Families.Select(f => f.Data.Item1).Order(), Registry.All.Select(d => d.Id).Order());

    [Theory]
    [MemberData(nameof(Families))]
    public void NamesMatch(string id, Type type)
    {
        var d = Registry.Get(id);
        Assert.Equal(LegacyConst(type, "name"), d.Name);
        Assert.Equal(LegacyConst(type, "longName"), d.LongName);
    }

    [Theory]
    [MemberData(nameof(Families))]
    public void ParameterTablesMatch(string id, Type type)
    {
        var legacy = CreateLegacy(type);
        var d = Registry.Get(id);
        Assert.Equal(legacy.engineData.Length, d.Parameters.Count);
        for (var i = 0; i < d.Parameters.Count; i++)
        {
            var l = legacy.engineData[i];
            var p = new ParameterDecoder(d.Parameters[i], d.Tables);
            Assert.Equal(l.Description, p.GetName(CultureInfo.InvariantCulture));
            Assert.Equal(l.Unit, p.GetUnit(CultureInfo.InvariantCulture));
            Assert.Equal(l.RequestSet, d.Parameters[i].Requests);
        }
    }

    /// <summary>Every parameter, every possible raw value: exactly the same decimal.</summary>
    [Theory]
    [MemberData(nameof(Families))]
    public void DecodedValuesAreIdenticalForEveryRawValue(string id, Type type)
    {
        var legacy = CreateLegacy(type);
        var d = Registry.Get(id);
        var buffer = new RegisterBuffer();
        for (var i = 0; i < d.Parameters.Count; i++)
        {
            var l = legacy.engineData[i];
            var p = new ParameterDecoder(d.Parameters[i], d.Tables);
            var r = d.Parameters[i].Requests;
            var combinations = r.Length == 2 ? 65536 : 256;
            for (var raw = 0; raw < combinations; raw++)
            {
                Array.Clear(LegacyEcu.Buffer);
                buffer.Clear();
                if (r.Length == 2)
                {
                    Set(buffer, r[0], (byte)(raw >> 8));
                    Set(buffer, r[1], (byte)raw);
                }
                else
                {
                    Set(buffer, r[0], (byte)raw);
                }
                var expected = l.Value;
                var actual = p.Decode(buffer);
                if (actual != expected)
                    Assert.Fail($"{id} {d.Parameters[i].Id}: raw {raw} → IES2 {expected}, new {actual?.ToString() ?? "null"}");
                if (raw % 7 == 0 && l.FormattedValue != p.Format(buffer, CultureInfo.InvariantCulture))
                    Assert.Fail($"{id} {d.Parameters[i].Id}: raw {raw} → IES2 '{l.FormattedValue}', new '{p.Format(buffer, CultureInfo.InvariantCulture)}'");
            }
        }
    }

    private static void Set(RegisterBuffer buffer, byte register, byte value)
    {
        LegacyEcu.Buffer[register] = value;
        buffer.Store(register, value, true);
    }

    /// <summary>Random register contents: same active/verified/stored flags and reason text.</summary>
    [Theory]
    [MemberData(nameof(Families))]
    public void ErrorDecodingIsIdentical(string id, Type type)
    {
        var legacy = CreateLegacy(type);
        var d = Registry.Get(id);
        Assert.Equal(legacy.engineErrors.Length, d.Errors.Engine.Items.Count);
        Assert.Equal(legacy.immoErrors?.Length ?? 0, d.Errors.Immobilizer.Items.Count);
        Assert.Equal(legacy.engineErrReq, d.Errors.Engine.Requests);
        Assert.Equal(legacy.immoErrReq, d.Errors.Immobilizer.Requests);

        var random = new Random(42);
        var buffer = new RegisterBuffer();
        for (var round = 0; round < 3000; round++)
        {
            for (var reg = 0; reg < LegacyEcu.Buffer.Length; reg++) // IES2 buffer has 255 entries
            {
                var v = round switch
                {
                    0 => (byte)0x00,
                    1 => (byte)0xFF,
                    _ => (byte)random.Next(256),
                };
                Set(buffer, (byte)reg, v);
            }
            Compare(legacy.engineErrors, d.Errors.Engine, buffer, id, round);
            Compare(legacy.immoErrors ?? [], d.Errors.Immobilizer, buffer, id, round);
        }
    }

    private static void Compare(errorElement[] legacy, ErrorGroup group, RegisterBuffer buffer, string id, int round)
    {
        for (var i = 0; i < legacy.Length; i++)
        {
            legacy[i].Decode();
            var n = ErrorDecoder.Decode(group.Scheme, group.Items[i], buffer, CultureInfo.InvariantCulture);
            var where = $"{id} error {i} ({group.Items[i].NameKey}), round {round}";
            Assert.True(legacy[i].Description == n.GetName(CultureInfo.InvariantCulture), where + ": name");
            Assert.True(legacy[i].isActive == n.IsActive, where + ": active");
            Assert.True(legacy[i].isVerified == n.IsVerified, where + ": verified");
            Assert.True(legacy[i].isStored == n.IsStored, where + ": stored");
            Assert.True((legacy[i].Reason ?? "") == n.Reason, $"{where}: reason '{legacy[i].Reason}' vs '{n.Reason}'");
        }
    }

    [Theory]
    [MemberData(nameof(Families))]
    public void ActiveTestsClearCodesAndAdjustmentsMatch(string id, Type type)
    {
        var legacy = CreateLegacy(type);
        var d = Registry.Get(id);

        Assert.Equal(legacy.activeTest.Length, d.ActiveTests.Count);
        for (var i = 0; i < d.ActiveTests.Count; i++)
        {
            var (l, n) = (legacy.activeTest[i], d.ActiveTests[i]);
            Assert.Equal(l.Description, Localizer.Text(n.NameKey, n.Name, CultureInfo.InvariantCulture));
            Assert.Equal(l.EngineRunning, n.EngineRunning);
            Assert.Equal(l.TimeOut, n.TimeoutSeconds);
            Assert.Equal(l.RequestSet, n.Requests);
        }

        Assert.NotNull(d.ClearCodes);
        Assert.Equal(legacy.clearCodes.TimeOut, d.ClearCodes.TimeoutSeconds);
        Assert.Equal(legacy.clearCodes.RequestSet, d.ClearCodes.Requests);

        var adjustments = legacy.adjustments ?? [];
        Assert.Equal(adjustments.Length, d.Adjustments.Count);
        for (var i = 0; i < adjustments.Length; i++)
        {
            var (l, n) = (adjustments[i], d.Adjustments[i]);
            Assert.Equal(l.Description, Localizer.Text(n.NameKey, n.Name, CultureInfo.InvariantCulture));
            Assert.Equal(l.StatusByte, n.StatusRequest);
            Assert.Equal(l.StatusMask, n.StatusMask);
            Assert.Equal(l.PreSet, n.PreSet);
            Assert.Equal(l.PostSet, n.PostSet);
            Assert.Equal(l.Type, n.Type);
        }
    }

    public static IEnumerable<string> CandidateIsoCodes()
    {
        var all = Registry.All.SelectMany(d => d.Vehicles.Select(v => v.Iso)).ToHashSet();
        all.Add("55D085??????");
        var random = new Random(7);
        foreach (var d in Registry.All)
            foreach (var m in d.Detection.IsoMatches)
                for (var i = 0; i < 20; i++)
                {
                    var hex = Convert.ToHexString(Enumerable.Range(0, 6).Select(_ => (byte)random.Next(256)).ToArray());
                    all.Add(hex[..m.Offset] + m.Value + hex[(m.Offset + m.Value.Length)..]);
                }
        for (var i = 0; i < 300; i++)
            all.Add(Convert.ToHexString(Enumerable.Range(0, 6).Select(_ => (byte)random.Next(256)).ToArray()));
        return all;
    }

    [Theory]
    [MemberData(nameof(Families))]
    public void IsoDetectionMatches(string id, Type type)
    {
        var d = Registry.Get(id);
        var check = type.GetMethod("CheckISO", BindingFlags.Public | BindingFlags.Static)!;
        foreach (var iso in CandidateIsoCodes())
        {
            LegacyEcu.ISO = iso;
            LegacyEcu.CODRIC = "6160206301"; // IES2's IAW-16F check also requires a CODRIC to have been read
            var legacyMatch = (bool)check.Invoke(null, null)!;
            var newMatch = d.Detection.AutoDetect && d.Detection.IsoMatches.Any(m => m.IsMatch(iso));
            if (d.Detection.AutoDetect)
                Assert.True(legacyMatch == newMatch, $"{id}: ISO {iso} → IES2 {legacyMatch}, new {newMatch}");
        }
    }

    /// <summary>IES2 tried the families in a fixed order (Connected() in frmMain.cs).</summary>
    [Fact]
    public void AutoDetectionPicksTheSameFamilyAsIes2()
    {
        Type[] legacyOrder = [typeof(iaw18fd), typeof(iaw18f), typeof(iaw8f_68), typeof(iaw16f), typeof(iaw04k)];
        foreach (var iso in CandidateIsoCodes())
        {
            LegacyEcu.ISO = iso;
            LegacyEcu.CODRIC = "6160206301";
            var legacy = legacyOrder.FirstOrDefault(t => (bool)t.GetMethod("CheckISO")!.Invoke(null, null)!);
            var expected = legacy is null ? null : LegacyConst(legacy, "name");
            var actual = Registry.AutoDetectOrder.FirstOrDefault(d => d.Detection.IsoMatches.Any(m => m.IsMatch(iso)))?.Id;
            Assert.True(expected == actual, $"ISO {iso}: IES2 {expected ?? "none"}, new {actual ?? "none"}");
        }
    }

    [Theory]
    [MemberData(nameof(Families))]
    public void VehicleLookupMatches(string id, Type type)
    {
        var legacy = CreateLegacy(type);
        var d = Registry.Get(id);
        foreach (var iso in CandidateIsoCodes())
        {
            LegacyEcu.ISO = iso;
            var expected = legacy.GetCarModel();
            var actual = d.Vehicles.FirstOrDefault(v => v.Iso == iso)?.Model ?? Localizer.Get("UnknownISO", CultureInfo.InvariantCulture);
            Assert.Equal(expected, actual);
        }
    }
}
