# Actuator tests and adjustments

[← Manual](README.md) · Previous: [Errors](errors.md) · Next: [Graphs →](graphs.md)

These two tabs send **active commands**: the ECU drives a real component, or changes a value in its memory. Read this page before using them.

## Safety

- Work with the car **parked**, handbrake on, gearbox in neutral, in a ventilated place, away from flames and fuel vapours.
- Keep the **ignition on and the engine stopped** unless the *Condition* column says otherwise. All the tests of the built-in families are done with the engine stopped.
- **Fuel pump and injector tests deliver fuel.** Never run them with fuel lines, injectors or the fuel rail disconnected. **Coil tests produce high voltage**: keep away from the HT leads.
- Every command asks for confirmation. To disable them altogether, untick **Enable active commands** in [Options](options.md); the buttons then only show a message.
- ECU families not marked *verified* never run active commands.
- The program has not been tested on a real car yet: leave active commands for last, after reading parameters and errors work.

While an active command runs, the status bar says *Command in progress...*, live readings pause, and the other command buttons are disabled.

## Actuator tests

Each row is a component the ECU can drive:

| Column | Meaning |
| --- | --- |
| **Name** | The component: fuel pump relay, injectors (all or one at a time), ignition coils, idle control valve, canister valve, A/C compressor relay, main relay, check-engine light (MIL), EGR valve, rev counter, consumption meter, overboost valve… |
| **Condition** | What the car must be like: *ignition on, engine stopped* or *engine running*. |
| **Max duration** | How long the ECU drives the component at most, for example 30 s for the fuel pump, 5 s for an injector, 2 s for the rev counter. |
| **Result** | The outcome of the last run. |

To run a test:

1. Satisfy the condition (key on MAR, engine stopped).
2. Click **Run** on the row.
3. The confirmation says what will be driven, for how long, and the condition. Click to confirm.
4. Listen or watch: the pump hums, an injector clicks, the rev counter needle moves, the MIL blinks…
5. The **Result** column shows the outcome.

| Result | Meaning |
| --- | --- |
| **PASSED** | The ECU ran the test and reported success. |
| **FAILED** | The ECU ran the test and reported a fault, or did not accept the command. |
| **TIMEOUT** | The ECU did not report the end of the test within the maximum duration. |
| **INIT. FAIL** | The ECU did not enter active mode, or did not echo the command. |
| **CANCELLED** | The test was interrupted (for example by disconnecting). |

The available tests depend on the ECU: see the [ECU reference](ecu-reference.md). The IAW-04K.P8, for example, can drive each injector and the overboost valve; the IAW-18FD/1G7 the EGR valve.

## Adjustments

Adjustments change settings stored in the ECU. The **Type** column says which kind each one is:

| Type | What happens when you click **Apply** |
| --- | --- |
| **on/off** | After a confirmation, the ECU switches the function or runs the reset. |
| **new value** | A window shows the current value with a slider and a number field (0–255): choose the new value and click **Apply**, or **Cancel**. |
| **code** | Sends a fixed code, after a confirmation. |

The adjustments of the built-in families:

- **Toggle fuel trim autocalibration** / **Toggle idle stepper autocalibration** — switch the mixture or idle self-adaptation on or off. The **Status** lights on the dashboard (*Mixture AutoCalibration ENABLED*, *Stepper AutoCalibration ENABLED*) show the current state. Switch it off to tune a car by hand without the ECU correcting you; switch it back on afterwards.
- **Fuel trim autocalibration reset** / **Idle stepper autocalibration reset** — erase what the ECU has learned. Useful after replacing a lambda probe, an injector, the idle stepper or the throttle body, or after cleaning. After a reset let the engine idle until it is warm, then drive normally for a few kilometres so the ECU learns again.
- **Set trimmer position (TRIMRAM)** and **Set stepper correction (ALFAFTR)** — only on the IAW-6F/16F: a new value from 0 to 255. Note the current value before changing it, so you can go back. On single-point engines the CO trimmer adjusts the idle mixture, and should be checked with an exhaust gas analyser.

The **Result** column shows the outcome as for the tests.

The exact names in your language and the list for each ECU are in the [ECU reference](ecu-reference.md); what *self-adaptation*, *trimmer* and *stepper* mean is in the [Glossary](glossary.md).
