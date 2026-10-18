# Dashboard

[← Manual](README.md) · Previous: [The main window](main-window.md) · Next: [Parameters →](parameters.md)

The dashboard shows the state of the engine at a glance, with the live parameters grouped by system and drawn as instruments.

![Dashboard](../../screenshots/dashboard.png)

## Vehicle and ECU card

The card at the top left describes the ECU family and, once connected, the car.

| Line | Meaning |
| --- | --- |
| **Detected vehicle** | Car and engine recognised from the ISO code, from the tables of IES2. If the code is not in the table: *ISO code … is not in the vehicle table* (the program still works: only the name of the car is missing). |
| ECU name | For example *Weber-Marelli IAW-04K.P8 MPI*. |
| **Technology** | What the system is, for example single-point injection for small engines. |
| **Also known as** | Other names of the same unit, as printed on its label (for example *IAW 4WF.P8*). |
| **Period** | Years of production of the cars that use it. |
| **Compatible vehicles** | Expand it to see, brand by brand, the models and engines that use this family. Marked *indicative, to be verified* when the list has not been confirmed. Underneath, **Recognised by ISO code** lists the cars the program can name automatically. |

Before connecting, the card shows the family chosen in the **ECU** list: a quick way to find out which family a car uses, without the car.

![Vehicle card](../../screenshots/vehicles.png)

## Groups

The other cards group the parameters by system. Each ECU family has its own parameters (see the [ECU reference](ecu-reference.md)); groups with nothing to show are left out.

| Group | Typical content |
| --- | --- |
| **Engine** | Engine speed, injection time, ignition advance, manifold pressure, injection timing |
| **Temperatures** | Coolant and intake air temperature |
| **Electrical** | Battery voltage |
| **Mixture and lambda** | Lambda correction, lambda probe voltage |
| **Throttle and idle** | Throttle angle, idle stepper position and gains, target idle speed, VAE duty cycle |
| **Self-adaptation** | CO trimmer, mixture and idle self-adaptation values |
| **Status** | Status lights: engine running, closed loop, A/C on, gear engaged… |
| **Other** | Anything else the definition provides |

## Instruments

- **Round gauges** for the main values (engine speed, manifold pressure, coolant temperature, battery voltage). The coloured arcs are the warning (yellow) and danger (red) zones, for example over-revving or a battery voltage that is too low or too high.
- **Bars** with the value and unit on the right. Bars for corrections that can go both ways (lambda correction, CO trimmer, self-adaptation offsets, and the idle stepper on the IAW-04K.P8) fill from the centre: to the right for a positive correction, to the left for a negative one.
- **Values**: small tiles with the number and unit, for parameters that have no natural scale (gains, offsets, target idle speed).
- **Status lights**: grey when off; **green** when on; **blue** when on for an information-only state (for example A/C on, gear engaged); **amber** for a state that deserves attention (ignition advance reduced for knocking).

A value shown as **#ERR** could not be decoded in the last reading.

## Good to know

- The dashboard shows only the parameters ticked in the **Read** column of [Parameters](parameters.md). By default all are ticked; unticking the ones you do not need makes the others refresh faster.
- The instruments scale with the window, between 80% and 150% of their size, so that everything fits the screen without scrolling. On a small screen the window scrolls instead.
- Names follow the language chosen in [Options](options.md), and so do decimal separators.
- The dark theme suits a dim garage: choose it with the theme button next to Options.

![Dark theme](../../screenshots/dashboard-dark.png)
