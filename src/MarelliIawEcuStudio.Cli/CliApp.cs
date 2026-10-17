using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using MarelliIawEcuStudio.Core.Decoding;
using MarelliIawEcuStudio.Core.Definitions;
using MarelliIawEcuStudio.Core.KLine;
using MarelliIawEcuStudio.Core.Obd;
using MarelliIawEcuStudio.Core.Localization;
using MarelliIawEcuStudio.Core.Logging;
using MarelliIawEcuStudio.Core.Protocol;
using MarelliIawEcuStudio.Core.Session;
using MarelliIawEcuStudio.Core.Transport;
using MarelliIawEcuStudio.Simulator;
using MarelliIawEcuStudio.Transport.Network;
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
          trace <file>                 Summarise a line trace written with --trace
          bridge                       Share the cable (or the simulator) on the network with RFC 2217
          obd <identify|read|dtc|clear>  OBD-II / EOBD over the K-Line, for cars of 2001 and later

        Connection:
          --port <name>                Serial port, e.g. COM3, /dev/ttyUSB0, or a network
                                       bridge: rfc2217://192.168.4.1:2217
          --simulate [ecu]             Use the built-in simulator instead of a cable
          --replay <trace>             Answer with the bytes of a line trace instead of a cable
          --replay-mode <strict|lookup>  strict: same requests in the same order (default);
                                       lookup: any request, answered with the recorded values
          --ecu <id|code|auto>         ECU family or label code, e.g. IAW-04K.P8 or 4WF.P8 (default: auto)
          --compat                     Slower timing for unreliable adapters (IES2 compatibility mode)
          --no-echo                    Adapter does not echo sent bytes
          --no-broadcast               Do not wait 10 s for the ISO code broadcast at key-on
          --dtr                        Raise DTR (some cables power the K-Line chip from it)
          --definitions <folder>       Extra definition files (override built-in ones)
          --lang <code>                Language: en, it, de, es, pl, pt, nl, hu, hr, sr
          --trace <file>               Write every byte on the line to a trace file (for bug reports)

        read:
          --params <id,id|all>         Parameters to read (default: all)
          --count <n>                  Number of cycles, 0 = until Ctrl+C (default: 1)
          --csv <file>                 Also write a CSV log
          --no-reconnect               Stop when communication is lost instead of reconnecting
          --reconnect-attempts <n>     Give up after n attempts (default: 0, keep trying)

        obd:
          --protocol <auto|iso9141|kwp-slow|kwp-fast>  K-Line protocol (default: auto)
          --simulate [iso9141|kwp-slow|kwp-fast]      Simulated OBD car
          --params <id,id|all>         read: PIDs such as RPM,ECT,MAP or 0C,05 (default: all supported)
          --count <n>                  read: number of cycles, 0 = until Ctrl+C (default: 1)

        bridge:
          --listen <[address:]port>    Where to listen (default 127.0.0.1:2217; use 0.0.0.0:2217
                                       to accept other computers on the network)

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
                "trace" => TraceSummary(a),
                "bridge" => Bridge(a, cancellation),
                "obd" => Obd(a, cancellation),
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
                                       or DefinitionException or InvalidDataException or KLineException)
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
            foreach (var alias in d.Aliases)
                output.WriteLine($"    also known as {alias.Name}{(alias.Note is null ? "" : " — " + alias.Note)}");
            if (d.Family is { } family)
            {
                output.WriteLine($"    {family.Technology?.Get()} ({family.Period})");
                foreach (var c in family.Compatibility)
                    output.WriteLine($"    ~ {c.Brand} {c.Model}{(c.Engines is null ? "" : " — " + c.Engines)}");
            }
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

        if (a.Get("replay") is { } replay)
        {
            var mode = a.Get("replay-mode") switch
            {
                null or "strict" => TraceReplayMode.Strict,
                "lookup" => TraceReplayMode.Lookup,
                var m => throw new CliException($"Unknown --replay-mode '{m}': use strict or lookup."),
            };
            var recorded = LineTraceReader.Read(replay);
            transport = new TraceReplayTransport(recorded, mode, Path.GetFileName(replay));
            timing = ProtocolTiming.Fast with { IsoBroadcastWaitMs = listen ? 1000 : 0, IsoBroadcastReadTimeoutMs = 200 };
            output.WriteLine($"Replay: {replay} ({recorded.Events.Count} events, {mode.ToString().ToLowerInvariant()})");
        }
        else if (a.IsSet("simulate"))
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
            transport = OpenPort(a);
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

        using var trace = a.Get("trace") is { } tracePath ? CreateTrace(tracePath, a, transport) : null;
        if (trace is not null)
        {
            session.Link.Monitor += trace.Write;
            output.WriteLine($"Line trace: {a.Get("trace")}");
        }
        try
        {
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
        finally
        {
            trace?.Comment("line: " + session.Link.Statistics);
        }
    }

    private static ITransport OpenPort(CliArguments a)
    {
        var port = a.Get("port") ?? throw new CliException("Give --port <name> (see 'iaw-cli ports') or --simulate.");
        if (port.StartsWith(Rfc2217Transport.Scheme, StringComparison.OrdinalIgnoreCase))
        {
            if (!Rfc2217Transport.TryParseAddress(port, out var host, out var tcpPort))
                throw new CliException($"Bad bridge address '{port}': use rfc2217://host:port.");
            return new Rfc2217Transport(host, tcpPort) { DtrEnable = a.Has("dtr") };
        }
        return new SerialTransport(port, dtrEnable: a.Has("dtr"));
    }

    private int Bridge(CliArguments a, CancellationToken cancellation)
    {
        var listen = a.Get("listen") ?? "127.0.0.1:2217";
        var colon = listen.LastIndexOf(':');
        var address = colon > 0 ? listen[..colon] : "127.0.0.1";
        if (!int.TryParse(colon >= 0 ? listen[(colon + 1)..] : listen, out var tcpPort) || tcpPort is < 0 or > 65535
            || !System.Net.IPAddress.TryParse(address.Trim('[', ']'), out var ip))
            throw new CliException($"Bad --listen '{listen}': use port or address:port, e.g. 0.0.0.0:2217.");

        Func<ITransport> open;
        if (a.IsSet("simulate"))
        {
            var registry = LoadRegistry(a);
            var definition = registry.Get(a.Get("simulate") is { Length: > 0 } s ? s : "IAW-6F/16F");
            var broadcast = !a.Has("no-broadcast");
            open = () => new SimulatorTransport(new SimulatedEcu(definition, new SimulatedEcuOptions { BroadcastIso = broadcast }))
            {
                Scenario = EngineScenario.Drive(),
            };
            output.WriteLine($"Sharing the simulator ({definition.LongName})");
        }
        else
        {
            var port = a.Get("port") ?? throw new CliException("Give --port <name> or --simulate.");
            if (port.StartsWith(Rfc2217Transport.Scheme, StringComparison.OrdinalIgnoreCase))
                throw new CliException("The bridge shares a local cable: give a serial port.");
            open = () => new SerialTransport(port, dtrEnable: a.Has("dtr"));
            output.WriteLine($"Sharing {port}");
        }

        var log = TextWriter.Synchronized(output);
        using var server = new Rfc2217Server(open, new System.Net.IPEndPoint(ip, tcpPort))
        {
            Log = m => log.WriteLine($"{DateTime.Now:HH:mm:ss} {m}"),
        };
        server.Start();
        var endpoint = server.LocalEndpoint;
        log.WriteLine($"Listening on {endpoint}. Connect with --port {Rfc2217Transport.Scheme}<this computer>:{endpoint.Port}. Ctrl+C to stop.");
        if (!System.Net.IPAddress.IsLoopback(ip))
            log.WriteLine("Warning: anyone on this network can now talk to the car. Use a private network.");
        cancellation.WaitHandle.WaitOne();
        log.WriteLine("Bridge stopped.");
        return 0;
    }

    private static KLineProtocol? ParseKLineProtocol(string? name, string option) => name switch
    {
        null or "" or "auto" => null,
        "iso9141" => KLineProtocol.Iso9141,
        "kwp-slow" => KLineProtocol.Kwp2000Slow,
        "kwp-fast" => KLineProtocol.Kwp2000Fast,
        _ => throw new CliException($"Unknown {option} '{name}': use iso9141, kwp-slow or kwp-fast."),
    };

    private int Obd(CliArguments a, CancellationToken cancellation)
    {
        var sub = a.Positional.Count > 0 ? a.Positional[0] : "identify";
        if (sub is not ("identify" or "read" or "dtc" or "clear"))
            throw new CliException($"Unknown obd command '{sub}': use identify, read, dtc or clear.");
        ITransport transport;
        var timing = KLineTiming.Default;
        if (a.IsSet("simulate"))
        {
            var simulated = ParseKLineProtocol(a.Get("simulate"), "--simulate") ?? KLineProtocol.Iso9141;
            timing = KLineTiming.Fast;
            var ecu = new SimulatedObdEcu();
            ecu.StoredCodes.Add(Dtc.Parse("P0420"));
            transport = new KLineSimulatorTransport(simulated, timing, ecu) { Echo = !a.Has("no-echo") };
            output.WriteLine($"OBD simulator ({simulated})");
        }
        else
        {
            transport = OpenPort(a);
        }

        using var session = new Obd2Session(transport, new Obd2Options
        {
            Timing = timing,
            AdapterEchoes = !a.Has("no-echo"),
            AllowClear = a.Has("allow-active"),
        });
        using var trace = a.Get("trace") is { } tracePath ? CreateTrace(tracePath, a, transport) : null;
        if (trace is not null)
            session.Link.Monitor += trace.Write;

        output.WriteLine("Waking up the ECU (the 5-baud init takes about 2 seconds)...");
        var c = session.Connect(ParseKLineProtocol(a.Get("protocol"), "--protocol"), cancellation);
        output.WriteLine($"Protocol:  {c.ProtocolName}, keywords {c.Keywords}");
        output.WriteLine($"ECUs:      {string.Join(", ", c.Ecus.Select(e => $"0x{e:X2}"))}");
        output.WriteLine($"PIDs:      {c.SupportedPids.Count} supported, {session.SupportedKnownPids().Count} known to this program");
        output.WriteLine();

        switch (sub)
        {
            case "identify":
                output.WriteLine($"VIN:         {session.ReadVin() ?? "not reported"}");
                output.WriteLine($"Calibration: {session.ReadCalibrationId() ?? "not reported"}");
                foreach (var p in session.SupportedKnownPids())
                    output.WriteLine($"  {p.Pid:X2}  {p.Id,-9} {p.GetName()}");
                return 0;
            case "read":
                return ObdRead(a, session, cancellation);
            case "dtc":
                var report = session.ReadDtcs();
                output.WriteLine($"MIL: {(report.Mil ? "ON" : "off")}, {report.ReportedCount} stored code(s) reported");
                foreach (var d in report.Stored)
                    output.WriteLine($"  stored   {d}{(d.IsGeneric ? "" : "  (manufacturer specific)")}");
                foreach (var d in report.Pending)
                    output.WriteLine($"  pending  {d}{(d.IsGeneric ? "" : "  (manufacturer specific)")}");
                if (report.Stored.Count == 0 && report.Pending.Count == 0)
                    output.WriteLine("No trouble codes.");
                return 0;
            default:
                if (!Confirm(a, "Clear OBD codes and freeze frames? The readiness monitors are reset too. Ignition on, engine stopped."))
                    return 1;
                var cleared = session.ClearDtcs();
                output.WriteLine(cleared ? "Codes cleared." : "The ECU did not confirm.");
                return cleared ? 0 : 1;
        }
    }

    private int ObdRead(CliArguments a, Obd2Session session, CancellationToken cancellation)
    {
        var list = a.Get("params");
        var pids = list is null or "all"
            ? session.SupportedKnownPids()
            : [.. list.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(k => ObdPids.Find(k) ?? throw new CliException($"Unknown PID '{k}'. Known: {string.Join(", ", ObdPids.All.Select(p => p.Id))}"))];
        if (pids.Count == 0)
            throw new CliException("The car reports none of the PIDs this program knows.");
        var count = a.GetInt("count", 1);
        var width = pids.Max(p => p.GetName().Length);
        var several = session.Connection!.Ecus.Count > 1;
        var clock = Stopwatch.StartNew();
        for (var cycle = 0; count == 0 || cycle < count; cycle++)
        {
            cancellation.ThrowIfCancellationRequested();
            var readings = session.Read(pids);
            if (count != 1)
                output.WriteLine($"--- {clock.Elapsed.TotalSeconds:0.0} s");
            foreach (var r in readings)
            {
                var ecu = several ? $"  [ECU 0x{r.Ecu:X2}]" : "";
                output.WriteLine($"{r.Pid.GetName().PadRight(width)}  {r.Text,10} {r.Pid.Unit}{ecu}");
            }
            foreach (var missing in pids.Where(p => readings.All(r => r.Pid != p)))
                output.WriteLine($"{missing.GetName().PadRight(width)}  {"-",10} (no answer)");
        }
        return 0;
    }

    private static LineTraceWriter CreateTrace(string path, CliArguments a, ITransport transport) =>
        LineTraceWriter.Create(path,
        [
            new("app", "iaw-cli " + Version),
            new("transport", transport.Name),
            new("os", System.Runtime.InteropServices.RuntimeInformation.OSDescription),
            new("arguments", string.Join(' ', a.Raw)),
        ]);

    private int Read(CliArguments a, DiagnosticSession session, CancellationToken cancellation)
    {
        var selected = SelectParameters(a.Get("params"), session);
        var count = a.GetInt("count", 1);
        using var csv = a.Get("csv") is { } path ? CsvLogWriter.Create(path, session.Connection!, selected, Version) : null;
        var request = new PollRequest { Parameters = selected };
        var width = selected.Max(p => p.GetName().Length);
        var clock = Stopwatch.StartNew();
        var reconnect = !a.Has("no-reconnect") && session.Transport is not TraceReplayTransport;
        var attempts = a.GetInt("reconnect-attempts", 0);
        for (var cycle = 0; count == 0 || cycle < count; cycle++)
        {
            cancellation.ThrowIfCancellationRequested();
            try
            {
                session.PollOnce(request);
            }
            catch (CommunicationLostException) when (session.Transport is TraceReplayTransport { IsExhausted: true })
            {
                output.WriteLine($"End of the trace after {cycle} cycles.");
                return 0;
            }
            catch (CommunicationLostException ex) when (reconnect)
            {
                output.WriteLine($"Communication lost: {LineFaultText.Describe(ex.Fault)}.");
                output.WriteLine("Reconnecting (Ctrl+C to stop)...");
                var outcome = session.Recover(cancellation, attempts, n => output.WriteLine($"  attempt {n}"));
                if (outcome == ReconnectOutcome.DifferentEcu)
                    throw new CliException("A different ECU answers (CODRIC changed). Run the command again.");
                if (outcome != ReconnectOutcome.Restored)
                    throw new CommunicationLostException(ex.Fault);
                output.WriteLine("Reconnected.");
                cycle--; // this cycle produced no data
                continue;
            }
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

    private int TraceSummary(CliArguments a)
    {
        if (a.Positional.Count == 0)
            throw new CliException("Give a trace file.");
        var trace = LineTraceReader.Read(a.Positional[0]);
        foreach (var (key, value) in trace.Metadata)
            output.WriteLine($"{key,-10} {value}");
        var events = trace.Events;
        var duration = events.Count > 0 ? events[^1].TimeMs / 1000 : 0;
        var echoes = events.Where(e => e.Direction == LineDirection.Echo).ToList();
        var mismatches = 0;
        for (var i = 1; i < events.Count; i++)
        {
            if (events[i].Direction == LineDirection.Echo && events[i - 1].Direction == LineDirection.Sent && events[i].Value != events[i - 1].Value)
                mismatches++;
        }
        output.WriteLine();
        output.WriteLine($"Duration:  {duration:0.0} s, {events.Count} events");
        output.WriteLine($"Requests:  {trace.RequestCount} at 7680 baud, {trace.TimeoutCount} without answer, {mismatches} with wrong echo");
        output.WriteLine($"Echo:      {(echoes.Count > 0 ? "yes" : "no")}");
        output.WriteLine();
        output.WriteLine("Markers and timeouts:");
        var shown = 0;
        foreach (var e in events.Where(e => e.Direction is LineDirection.Marker or LineDirection.Timeout))
        {
            if (shown++ >= 200)
            {
                output.WriteLine("  ...");
                break;
            }
            output.WriteLine(e.Direction == LineDirection.Marker
                ? $"  {e.TimeMs / 1000,9:0.000} s  {e.Note}"
                : $"  {e.TimeMs / 1000,9:0.000} s  no answer to 0x{e.Value:X2} at {e.BaudRate} baud");
        }
        return 0;
    }

    private void ReportIssues(DefinitionRegistry registry)
    {
        foreach (var i in registry.Issues)
            output.WriteLine($"Warning: skipped definition {i}");
    }
}
