using MarelliIawEcuStudio.Core.Definitions;
using MarelliIawEcuStudio.Core.Transport;
using MarelliIawEcuStudio.Presentation.Localization;
using MarelliIawEcuStudio.Simulator;
using MarelliIawEcuStudio.Transport.Network;
using MarelliIawEcuStudio.Transport.Serial;

namespace MarelliIawEcuStudio.Presentation.Services;

/// <summary>Something the application can connect to: a serial port or the simulator.</summary>
public sealed record ConnectionTarget(string Id, string Name, string Description, bool IsSimulator, string? Hint = null)
{
    public const string SimulatorId = "simulator";

    /// <summary>A network bridge target for <c>rfc2217://host:port</c>, or null when the address is not valid.</summary>
    public static ConnectionTarget? Bridge(string? address)
    {
        if (string.IsNullOrWhiteSpace(address))
            return null;
        var a = address.Trim();
        if (!a.Contains("://", StringComparison.Ordinal))
            a = Rfc2217Transport.Scheme + a;
        return Rfc2217Transport.TryParseAddress(a, out var host, out var port)
            ? new ConnectionTarget($"{Rfc2217Transport.Scheme}{host}:{port}", $"{host}:{port}", Ui.Get("BridgeDescription"), false)
            : null;
    }

    public override string ToString() => Description.Length > 0 ? $"{Name} — {Description}" : Name;
}

public interface ITransportFactory
{
    IReadOnlyList<ConnectionTarget> ListTargets();

    /// <summary>Creates the transport. For the simulator, <paramref name="simulatedEcu"/> is the family to emulate.</summary>
    ITransport Create(ConnectionTarget target, EcuDefinition simulatedEcu, bool broadcastIso, bool dtrEnable);
}

/// <summary>Real serial ports plus the built-in simulator.</summary>
public sealed class DefaultTransportFactory : ITransportFactory
{
    public IReadOnlyList<ConnectionTarget> ListTargets()
    {
        var targets = new List<ConnectionTarget>();
        try
        {
            foreach (var p in PortDiscovery.List())
            {
                var description = string.Join(", ", new[] { p.Chip, p.Description }.Where(s => !string.IsNullOrEmpty(s)).Distinct());
                targets.Add(new ConnectionTarget(p.StableName ?? p.Name, p.Name, description, false, p.Hint));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // No access to the device list: only the simulator is offered.
        }
        targets.Add(new ConnectionTarget(ConnectionTarget.SimulatorId, Ui.Get("SimulatorName"), Ui.Get("SimulatorDescription"), true));
        return targets;
    }

    public ITransport Create(ConnectionTarget target, EcuDefinition simulatedEcu, bool broadcastIso, bool dtrEnable)
    {
        if (target.IsSimulator)
        {
            var ecu = new SimulatedEcu(simulatedEcu, new SimulatedEcuOptions { BroadcastIso = broadcastIso, BroadcastDelayMs = 800 });
            return new SimulatorTransport(ecu) { Scenario = EngineScenario.Drive() };
        }
        if (Rfc2217Transport.TryParseAddress(target.Id, out var host, out var port))
            return new Rfc2217Transport(host, port) { DtrEnable = dtrEnable };
        return new SerialTransport(target.Id, dtrEnable);
    }
}
