using MarelliIawEcuStudio.Core.Definitions;
using MarelliIawEcuStudio.Core.Transport;
using MarelliIawEcuStudio.Presentation.Localization;
using MarelliIawEcuStudio.Simulator;
using MarelliIawEcuStudio.Transport.Serial;

namespace MarelliIawEcuStudio.Presentation.Services;

/// <summary>Something the application can connect to: a serial port or the simulator.</summary>
public sealed record ConnectionTarget(string Id, string Name, string Description, bool IsSimulator, string? Hint = null)
{
    public const string SimulatorId = "simulator";

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
        return new SerialTransport(target.Id, dtrEnable);
    }
}
