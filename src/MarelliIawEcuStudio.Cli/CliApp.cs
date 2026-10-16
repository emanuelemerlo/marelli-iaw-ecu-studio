using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using MarelliIawEcuStudio.Core.Decoding;
using MarelliIawEcuStudio.Core.Definitions;
using MarelliIawEcuStudio.Core.Localization;
using MarelliIawEcuStudio.Core.Logging;
using MarelliIawEcuStudio.Core.Protocol;
using MarelliIawEcuStudio.Core.Session;
using MarelliIawEcuStudio.Core.Transport;
using MarelliIawEcuStudio.Simulator;
using MarelliIawEcuStudio.Transport.Serial;

namespace MarelliIawEcuStudio.Cli;

/// <summary>The <c>iaw-cli</c> commands. Read only unless <c>--allow-active</c> is given.</summary>
internal sealed class CliApp(TextWriter output, TextReader input)
{
    public static string Version =>
        typeof(CliApp).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] ?? "0.0.0";

    private const string Usage = """
        iaw-cli — diagnostics for Magneti Marelli IAW ECUs (FIAT/Lancia/Alfa Romeo OBD-I)

        Usage: iaw-cli <command> [options]

        Commands:
          ecus                         List known ECU families and their vehicles
          ports                        List serial ports (USB chip, stable name, latency)
          identify                     Connect and show ECU type, ISO and CODRIC codes
          read                         Read live parameters
          errors                       Read engine and immobiliser errors
          test <name|number>           Run an actuator test          (needs --allow-active)
          clear-codes                  Clear stored errors           (needs --allow-active)
          validate <file|folder>       Check ECU definition files

        Connection:
          --port <name>                Serial port, e.g. COM3, /dev/ttyUSB0
          --simulate [ecu]             Use the built-in simulator instead of a cable
          --ecu <id|auto>              ECU family (default: auto)
          --compat                     Slower timing for unreliable adapters (IES2 compatibility mode)
          --no-echo                    Adapter does not echo sent bytes
          --no-broadcast               Do not wait 10 s for the ISO code broadcast at key-on
          --dtr                        Raise DTR (some cables power the K-Line chip from it)
          --definitions <folder>       Extra definition files (override built-in ones)
          --lang <code>                Language: en, it, de, es, pl, pt, nl, hu, hr, sr

        read:
          --params <id,id|all>         Parameters to read (default: all)
          --count <n>                  Number of cycles, 0 = until Ctrl+C (default: 1)
          --csv <file>                 Also write a CSV log

        Safety:
          --allow-active               Allow actuator tests and clear codes
          --allow-unverified           Also for definitions not marked verified
          --yes                        Do not ask for confirmation
        """;

    public int Run(string[] args, CancellationToken cancellation = default)
    {
        try
        {
            var a = CliArguments.Parse(args);
            if (a.Get("lang") is { } lang)
                CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(lang);
            if (a.Has("help") || a.Command == "help")
            {
                output.WriteLine(Usage);
                return 0;
            }
            return a.Command switch
            {
                "ecus" => ListEcus(a),
                "ports" => ListPorts(),
                "identify" => WithSession(a, cancellation, s => 0),
                "read" => WithSession(a, cancellation, s => Read(a, s, cancellation)),
                "errors" => WithSession(a, cancellation, s => Errors(s)),
                "test" => WithSession(a, cancellation, s => Test(a, s, cancellation)),
                "clear-codes" => WithSession(a, cancellation, s => ClearCodes(a, s, cancellation)),
                "validate" => Validate(a),
                _ => throw new CliException($"Unknown command '{a.Command}'. Run 'iaw-cli help'."),
            };
        }
        catch (CliException ex)
        {
            output.WriteLine("Error: " + ex.Message);
            return 2;
        }
        catch (Exception ex) when (ex is UnrecognizedEcuException or CommunicationLostException or ActiveCommandNotAllowedException
                                       or UnauthorizedAccessException or IOException or NotSupportedException or KeyNotFoundException
                                       or DefinitionException)
        {
            output.WriteLine("Error: " + ex.Message);
            return 1;
        }
        catch (OperationCanceledException)
        {
            output.WriteLine("Cancelled.");
            return 130;
        }
    }

    private static DefinitionRegistry LoadRegistry(CliArguments a)
    {
        var dirs = new List<string> { DefinitionRegistry.DefaultUserDirectory };
        if (a.Get("definitions") is { } extra)
            dirs.Add(extra);
        return DefinitionRegistry.Load([.. dirs]);
    }

    private int ListEcus(CliArguments a)
    {
        var registry = LoadRegistry(a);
        ReportIssues(registry);
        foreach (var d in registry.All.OrderBy(d => d.Id, StringComparer.Ordinal))
        {
            output.WriteLine($"{d.Id,-14} {d.LongName}  [{d.Parameters.Count} parameters, {d.Errors.Engine.Items.Count} errors, {d.ActiveTests.Count} tests]{(d.Verified ? "" : "  UNVERIFIED")}");
            foreach (var v in d.Vehicles.Take(50))
                output.WriteLine($"    {v.Iso}  {v.Model}");
        }
        return 0;
    }

    private int ListPorts()
    {
        var ports = PortDiscovery.List();
        if (ports.Count == 0)
        {
            output.WriteLine("No serial ports found. Is the cable plugged in?");
            return 0;
        }
        foreach (var p in ports)
        {
            output.WriteLine($"{p.Name,-16} {p.Chip,-20} {p.Description}");
            if (p.StableName is not null)
                output.WriteLine($"{"",-16} stable name: {p.StableName}");
            if (p.Hint is not null)
                output.WriteLine($"{"",-16} hint: {p.Hint}");
        }
        return 0;
    }

    private int WithSession(CliArguments a, CancellationToken cancellation, Func<DiagnosticSession, int> action)
    {
        var registry = LoadRegistry(a);
        ReportIssues(registry);
        var ecu = a.Get("ecu") is { } e && !e.Equals("auto", StringComparison.OrdinalIgnoreCase) ? e : null;
        var timing = a.Has("compat") ? ProtocolTiming.Compatibility : ProtocolTiming.Default;
        ITransport transport;
        var listen = !a.Has("no-broadcast");

        if (a.IsSet("simulate"))
        {
            var id = a.Get("simulate") is { Length: > 0 } s ? s : ecu ?? "IAW-6F/16F";
            var sim = new SimulatedEcu(registry.Get(id), new SimulatedEcuOptions { BroadcastIso = listen });
            transport = new SimulatorTransport(sim) { Scenario = EngineScenario.Idle(), Echo = !a.Has("no-echo") };
            if (listen)
                timing = timing with { IsoBroadcastWaitMs = 2000 };
            output.WriteLine($"Simulator: {sim.Definition.LongName}, ISO {sim.Iso}");
        }
        else
        {
            var port = a.Get("port") ?? throw new CliException("Give --port <name> (see 'iaw-cli ports') or --simulate.");
            transport = new SerialTransport(port, dtrEnable: a.Has("dtr"));
        }

        var policy = !a.Has("allow-active") ? ActiveCommandPolicy.Disabled
            : a.Has("allow-unverified") ? ActiveCommandPolicy.All
            : ActiveCommandPolicy.VerifiedOnly;
        using var session = new DiagnosticSession(transport, registry, new SessionOptions
        {
            Timing = timing,
            AdapterEchoes = !a.Has("no-echo"),
            ListenForIsoBroadcast = listen,
            ActiveCommands = policy,
        });

        if (listen)
            output.WriteLine("Turn the ignition on now (waiting for the ECU)...");
        var c = session.Connect(ecu, cancellation);
        output.WriteLine($"ECU:     {c.EcuDisplayName}");
        output.WriteLine($"ISO:     {c.FormattedIso}");
        output.WriteLine($"CODRIC:  {c.FormattedCodric}");
        output.WriteLine($"Vehicle: {c.VehicleModel ?? Localizer.Get("UnknownISO")}");
        output.WriteLine();
        return action(session);
    }

    private int Read(CliArguments a, DiagnosticSession session, CancellationToken cancellation)
    {
        var selected = SelectParameters(a.Get("params"), session);
        var count = a.GetInt("count", 1);
        using var csv = a.Get("csv") is { } path ? CsvLogWriter.Create(path, session.Connection!, selected, Version) : null;
        var request = new PollRequest { Parameters = selected };
        var width = selected.Max(p => p.GetName().Length);
        var clock = Stopwatch.StartNew();
        for (var cycle = 0; count == 0 || cycle < count; cycle++)
        {
            cancellation.ThrowIfCancellationRequested();
            session.PollOnce(request);
            csv?.WriteSample(clock.ElapsedMilliseconds, session.Buffer);
            if (count != 1)
                output.WriteLine($"--- {clock.Elapsed.TotalSeconds:0.0} s");
            foreach (var p in selected)
            {
                var raw = a.Has("raw") ? $"  (raw {p.ReadRaw(session.Buffer)})" : "";
                output.WriteLine($"{p.GetName().PadRight(width)}  {p.Format(session.Buffer),10} {p.GetUnit()}{raw}");
            }
        }
        return 0;
    }

    private static List<ParameterDecoder> SelectParameters(string? list, DiagnosticSession session)
    {
        if (list is null or "all")
            return [.. session.Parameters];
        return [.. list.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(session.GetParameter)];
    }

    private int Errors(DiagnosticSession session)
    {
        session.PollOnce(new PollRequest { Errors = true });
        var present = session.EngineErrors().Concat(session.ImmobilizerErrors()).Where(e => e.IsPresent).ToList();
        if (present.Count == 0)
        {
            output.WriteLine("No errors.");
            return 0;
        }
        foreach (var e in present)
            output.WriteLine($"{e.GetName(),-40} {e.GetStatusText(),-4} {e.Reason}");
        return 0;
    }

    private int Test(CliArguments a, DiagnosticSession session, CancellationToken cancellation)
    {
        var tests = session.Definition.ActiveTests;
        if (a.Positional.Count == 0)
        {
            for (var i = 0; i < tests.Count; i++)
                output.WriteLine($"{i + 1,2}. {Localizer.Text(tests[i].NameKey, tests[i].Name)} ({tests[i].TimeoutSeconds} s)");
            output.WriteLine("Run: iaw-cli test <number> --allow-active");
            return 0;
        }
        var key = a.Positional[0];
        var test = int.TryParse(key, out var n) && n >= 1 && n <= tests.Count
            ? tests[n - 1]
            : tests.FirstOrDefault(t => string.Equals(t.NameKey ?? t.Name, key, StringComparison.OrdinalIgnoreCase))
              ?? throw new CliException($"No test '{key}'. Run 'iaw-cli test' to list them.");
        var name = Localizer.Text(test.NameKey, test.Name);
        if (!Confirm(a, $"Run '{name}'? The actuator will be driven for up to {test.TimeoutSeconds} s. " +
                        (test.EngineRunning ? "The engine must be running." : "Ignition on, engine stopped.")))
            return 1;
        var result = session.RunActiveTest(test, cancellation);
        output.WriteLine($"{name}: {result.Outcome}");
        return result.Outcome == ActiveOutcome.Passed ? 0 : 1;
    }

    private int ClearCodes(CliArguments a, DiagnosticSession session, CancellationToken cancellation)
    {
        if (!Confirm(a, "Clear all stored error codes? Ignition on, engine stopped."))
            return 1;
        var result = session.ClearCodes(cancellation);
        output.WriteLine($"Clear codes: {result.Outcome}");
        return result.Outcome == ActiveOutcome.Passed ? 0 : 1;
    }

    private bool Confirm(CliArguments a, string question)
    {
        if (!a.Has("allow-active"))
            throw new ActiveCommandNotAllowedException("Active commands are disabled. Add --allow-active to enable them.");
        if (a.Has("yes"))
            return true;
        output.Write(question + " [y/N] ");
        var answer = input.ReadLine();
        return answer is not null && answer.Trim().StartsWith('y');
    }

    private int Validate(CliArguments a)
    {
        if (a.Positional.Count == 0)
            throw new CliException("Give a definition file or folder.");
        var path = a.Positional[0];
        var issues = new List<DefinitionIssue>();
        IReadOnlyList<EcuDefinition> loaded;
        if (Directory.Exists(path))
        {
            loaded = DefinitionLoader.LoadDirectory(path, issues);
        }
        else
        {
            try
            {
                loaded = [DefinitionLoader.Parse(File.ReadAllText(path), path)];
            }
            catch (DefinitionException ex)
            {
                issues.AddRange(ex.Issues);
                loaded = [];
            }
        }
        foreach (var d in loaded)
            output.WriteLine($"OK      {d.Origin} ({d.Id})");
        foreach (var i in issues)
            output.WriteLine($"INVALID {i}");
        return issues.Count == 0 ? 0 : 1;
    }

    private void ReportIssues(DefinitionRegistry registry)
    {
        foreach (var i in registry.Issues)
            output.WriteLine($"Warning: skipped definition {i}");
    }
}
