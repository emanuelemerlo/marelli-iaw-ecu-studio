# marelli-iaw-ecu-studio libraries

Diagnostics for the Magneti Marelli IAW engine control units of 1990s FIAT, Lancia and Alfa Romeo cars (OBD-I, FIAT 3-pin connector), and OBD-II over the K-Line for later cars. These are the libraries behind the [marelli-iaw-ecu-studio](https://github.com/emanuelemerlo/marelli-iaw-ecu-studio) desktop application and the `iaw-cli` command line tool.

| Package | Content |
| --- | --- |
| `MarelliIawEcuStudio.Core` | IAW byte protocol, built-in ECU definitions (JSON), diagnostic session with reconnection, line traces, CSV logs; framed K-Line (ISO 9141-2, ISO 14230 KWP2000) and OBD-II |
| `MarelliIawEcuStudio.Transport.Serial` | KKL USB cables on Windows, Linux and macOS, with port discovery |
| `MarelliIawEcuStudio.Transport.Network` | RFC 2217 client and server: adapters on the network (`iaw-cli bridge`, ser2net, esp-link) |
| `MarelliIawEcuStudio.Simulator` | Simulated IAW and OBD ECUs, trace replay: everything works without a car |

## Read the engine speed of an IAW unit

```csharp
using MarelliIawEcuStudio.Core.Definitions;
using MarelliIawEcuStudio.Core.Session;
using MarelliIawEcuStudio.Transport.Serial;

var registry = DefinitionRegistry.BuiltIn();
using var session = new DiagnosticSession(new SerialTransport("/dev/ttyUSB0"), registry);
var ecu = session.Connect(ecuId: null);            // automatic detection; turn the ignition on now
Console.WriteLine($"{ecu.EcuDisplayName}, ISO {ecu.FormattedIso}");

var rpm = session.GetParameter("PERIODE");
session.PollOnce(new PollRequest { Parameters = [rpm] });
Console.WriteLine($"{rpm.Format(session.Buffer)} {rpm.GetUnit()}");
```

Replace `SerialTransport` with `new SimulatorTransport(new SimulatedEcu(registry.Get("IAW-8F/18F")))` to try it without a car.

## Read an OBD-II car

```csharp
using MarelliIawEcuStudio.Core.Obd;

using var obd = new Obd2Session(new SerialTransport("/dev/ttyUSB0"));
var car = obd.Connect();                            // 5-baud init, then fast init
foreach (var r in obd.Read(obd.SupportedKnownPids()))
    Console.WriteLine($"{r.Pid.Name}: {r.Text} {r.Pid.Unit}");
Console.WriteLine($"VIN {obd.ReadVin()}, codes: {string.Join(", ", obd.ReadDtcs().Stored)}");
```

## Safety

Actuator tests, clear codes and adjustments drive real components. Sessions refuse them unless `ActiveCommands` (IAW) or `AllowClear` (OBD) is set, and IAW definitions not marked `verified` stay read-only unless explicitly allowed.

Licensed under the GNU GPL v3 or later; parts derived from IES2 by Tomasz Orczyk remain also under its BSD 3-Clause licence (`LICENSE-IES2`, `NOTICE`). Not affiliated with Marelli, FIAT or their successors.
