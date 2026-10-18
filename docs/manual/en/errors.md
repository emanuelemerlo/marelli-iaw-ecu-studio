# Errors

[← Manual](README.md) · Previous: [Parameters](parameters.md) · Next: [Actuator tests and adjustments →](tests-and-adjustments.md)

The **Errors** tab lists the faults the ECU has detected on its sensors, actuators and internal circuits, and those of the FIAT CODE immobiliser when the car has one.

![Errors](../../screenshots/errors.png)

## Reading errors

With **Read errors** ticked (the default), the program reads the error registers every five cycles, between parameter readings. Only the errors that are present are listed; with none, the tab says *No errors*. Unticking **Read errors** makes the parameters refresh a little faster.

| Column | Meaning |
| --- | --- |
| **Name** | The component or function at fault: throttle potentiometer, MAP sensor, lambda probe, coolant temperature sensor, injector, ignition coil, idle stepper, canister valve, RAM/ROM/EEPROM… The full list for each ECU is in the [ECU reference](ecu-reference.md). |
| **Status** | One or more letters, see below. |
| **Reason** | Why, when the ECU says it: *Short to GND*, *Short to +Vcc*, *Open Circuit*, *Short to GND/Open*, *Above MAX*, *Below MIN*, *Max RICH*, *Max LEAN*, *Too Much Air*, *No Air*… |

### Status letters

| Letter | Name | Meaning |
| --- | --- | --- |
| **[A]** | active | The fault is present **now**. The row is shown in red. |
| **[V]** | verified | The ECU has confirmed the fault (not available on every family). |
| **[S]** | stored | The fault is in the ECU's memory: it happened at some point, and may no longer be present. |

The letters combine: **[A][S]** is a fault present now and already memorised; **[S]** alone is a past fault, for example a connector that was unplugged with the ignition on.

### Immobiliser errors

When the ECU bar shows *Immobiliser: present*, the list also includes the FIAT CODE errors: ECU not synchronised with the CODE unit, engine start not allowed, key code error, unrecognised code, no code received… They help with a car that cranks but does not start.

## Clearing codes

**Clear codes** erases the stored errors from the ECU's memory.

1. Ignition **on** (key on MAR), engine **stopped**.
2. Click **Clear codes** and confirm.
3. The status bar shows the outcome: **PASSED**, **FAILED**, **TIMEOUT**…

Things to know:

- It needs **Enable active commands** in [Options](options.md); otherwise the program says so and does nothing.
- Clear codes only after fixing the cause and noting the errors down (a screenshot is enough): an active fault comes back at once.
- Resetting the self-adaptation is a separate command, in [Adjustments](tests-and-adjustments.md#adjustments).

## A diagnostic routine

1. Connect with the engine off and note the errors.
2. Clear codes, then start the engine and let it idle.
3. Errors that come back with **[A]** are present faults: check wiring and component. Errors that never come back were intermittent or caused by past work on the car.
4. To find intermittent faults, watch the tab while wiggling connectors and wiring with the engine running.
