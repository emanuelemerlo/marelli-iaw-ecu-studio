# The main window

[← Manual](README.md) · Previous: [Installation](getting-started.md) · Next: [Dashboard →](dashboard.md)

![Main window](../../screenshots/parameters.png)

From top to bottom the window has:

1. the **connection bar**: interface, ECU, Connect, theme, Options;
2. a yellow **hint** line, only when there is something to tell about the port (for example a slow FTDI latency);
3. the **ECU bar**, only while connected: what the program is talking to;
4. the **tabs**: Dashboard, Parameters, Errors, Actuator tests, Adjustments, Graphs, Log replay;
5. the **status bar**: what is happening, the recording or trace file, the refresh rate.

The layout adapts to the window: the dashboard and the parameter table grow or shrink to fit without scrolling, down to a size that stays readable, and on a wide screen parameters spread over several columns.

## Connection bar

| Control | What it does |
| --- | --- |
| **Interface** | Where the ECU is: a serial port (the KKL cable), a network bridge set in [Options](options.md#network-bridge), or the **Simulator**. The program remembers the last one. |
| **Refresh** | Looks for ports again, for example after plugging in the cable. |
| **ECU** (search box) | Narrows the ECU list: type the code on the label, a car or a brand. |
| **ECU** (list) | **Automatic Detection** (recognises the family from the ISO code), or a specific family. Each entry has a second line with other names of the unit and a few vehicles. |
| **Connect** / **Cancel** / **Reconnect** / **Disconnect** | One button that changes with the state of the connection, see below. |
| **Theme** | Shows the current theme; the menu chooses *Same as the system*, *Light* or *Dark*. |
| **Options** | Language, theme and connection settings: see [Options](options.md). |

Interface and ECU can only be changed while disconnected.

### Choosing the ECU

**Automatic Detection** is the right choice almost always: the program reads the ISO code and picks the matching family. The connected ECU then appears in the ECU bar.

Choose a family by hand when:

- the program says *ECU could not be recognized*;
- your ECU is an IAW-04K.P8 (also IAW 4WF.P8) and the ignition is already on: this family is recognised automatically only from the ISO code it sends at key-on, so either keep **Wait for the ISO code at key-on** ticked and turn the key after clicking Connect, or pick the family by hand;
- you want to simulate a particular family.

The search box looks in the family name, its other names, its compatible cars and engines. Every word you type must match somewhere: `4wf p8`, `coupe turbo`, `alfa 33`, `punto 1.2 16v` all work. Typing a code selects the first family that matches it. Families not marked *verified* show **(UNVERIFIED)**: their active commands stay disabled.

### Connection states

| State | Button | Keyboard | Meaning |
| --- | --- | --- | --- |
| Not connected | **Connect** | F10 | Ready to start. |
| Connecting | **Cancel** | Esc | Waiting for the ignition, initialising the ECU. |
| Connected | **Disconnect** | — | Data is being read. |
| Communication lost | **Reconnect** and **Disconnect** | F12 | The ECU stopped answering. With automatic reconnection on, the program retries by itself; **Reconnect** tries at once. See [Troubleshooting](troubleshooting.md#lost-communication). |

## ECU bar

Shown while connected:

| Field | Meaning |
| --- | --- |
| **ECU** | Family of the connected unit, for example *Magneti-Marelli IAW-8F/18F MPI*. The suffix **ECOL** (as in IES2) marks a unit without immobiliser. |
| **ISO code** | Identification code of the software and calibration, for example `55-31-80-02-94-1C`. It decides the vehicle. |
| **CODRIC** | Hardware code of the unit. The program checks it when reconnecting, so a different ECU is never mistaken for the one connected. |
| **Vehicle** | Car and engine recognised from the ISO code, or *Unknown*. Hover to read it in full. |
| **Immobiliser** | *present* if a FIAT CODE immobiliser answers, otherwise *absent*. |
| **Simulation** | Badge shown when the data comes from the simulator. |

## Tabs

| Tab | Content | Page |
| --- | --- | --- |
| **Dashboard** | Vehicle card and live data as gauges, bars and lights, grouped by system. | [Dashboard](dashboard.md) |
| **Parameters** | All live values in a table: what to read, what to plot, CSV recording. | [Parameters](parameters.md) |
| **Errors** | Engine and immobiliser errors, clear codes. | [Errors](errors.md) |
| **Actuator tests** | Drive a component (pump, injector, coil…) for a few seconds. | [Tests and adjustments](tests-and-adjustments.md) |
| **Adjustments** | Enable, disable or reset self-adaptation; set trimmer and stepper values. | [Tests and adjustments](tests-and-adjustments.md#adjustments) |
| **Graphs** | Live plots of the chosen parameters. | [Graphs](graphs.md) |
| **Log replay** | Play back a CSV log, also without a car. | [Log replay](log-replay.md) |

Before connecting, the Dashboard shows the card of the family chosen in **ECU**, and **Log replay** is available; the other tabs are greyed out until the ECU answers.

## Status bar

- **Left**: the state (*Not connected*, *Please turn ON the ignition...*, *Connected: …*, *Command in progress...*), the result of the last command, or the reason communication was lost.
- **Middle**: the file being recorded (*Recording to …*), the last file saved (*Saved …*), or the line trace (*Line trace: …*).
- **Right**: the real refresh rate, in updates per second. The fewer parameters you read, the higher it gets.

## Keyboard shortcuts

| Key | Action |
| --- | --- |
| **F10** | Connect |
| **Esc** | Cancel the connection attempt |
| **F12** | Reconnect after a communication loss (as in IES2) |

In graphs: mouse wheel to zoom, drag to pan, double-click to reset the view.
