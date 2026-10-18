# Marelli IAW ECU Studio — User manual

[Leggi in italiano](../it/README.md)

Marelli IAW ECU Studio reads the Magneti Marelli IAW engine control units of 1990s FIAT, Lancia and Alfa Romeo cars through the FIAT 3-pin diagnostic connector: it recognises the ECU and the car, shows live engine data, reads and clears error codes, runs actuator tests and adjustments, records and replays logs.

This manual is for the person using the program, at home or in the workshop. It covers the desktop application and the `iaw-cli` command line tool. For building from source and for developers, see the [README](../../../README.md).

> **The program has not been tested on a real car yet.** Everything works with the built-in simulator and has been checked against the original IES2 code. Start with read-only operations, and please report your results (see [Troubleshooting](troubleshooting.md#reporting-a-problem)).

![Dashboard](../../screenshots/dashboard.png)

## Contents

**Getting started**

1. [Installation and first connection](getting-started.md) — download, start, try the simulator, connect to the car
2. [The main window](main-window.md) — connection bar, ECU bar, tabs, status bar, shortcuts

**Using the program**

3. [Dashboard](dashboard.md) — vehicle card, gauges, bars and status lights
4. [Parameters and CSV recording](parameters.md) — the live table, what to read, what to record
5. [Errors](errors.md) — reading error codes, what [A], [V], [S] mean, clearing codes
6. [Actuator tests and adjustments](tests-and-adjustments.md) — driving components, self-adaptation reset, trimmer
7. [Graphs](graphs.md) — live plots, zoom, scales, time window
8. [Log replay](log-replay.md) — playing back CSV logs, also from IES2
9. [Options](options.md) — every setting, and where files are saved

**When something goes wrong**

10. [Troubleshooting](troubleshooting.md) — messages, lost communication, line trace, reporting a problem

**Reference**

11. [Command line (`iaw-cli`)](command-line.md) — every command and option
12. [ECU reference](ecu-reference.md) — parameters, errors, tests and adjustments of each ECU family
13. [Glossary](glossary.md) — ISO code, CODRIC, self-adaptation, stepper, K-Line and other terms
14. [Hardware](../../hardware.md) — cable, adapter, drivers, latency, network bridge, OBD-II

## Safety first

Actuator tests, clearing codes and adjustments drive real components: fuel pump, injectors, ignition coils, relays. Before each one the program asks for confirmation and says the condition required (usually ignition on, engine stopped). Work with the car parked, in a ventilated place, away from fuel vapours, and never run an injector or fuel pump test with fuel lines open. Active commands can be turned off altogether in [Options](options.md). Use at your own risk: see the warranty disclaimer in the [license](../../../LICENSE).
