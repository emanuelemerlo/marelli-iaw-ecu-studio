# ECU reference

[← Manual](README.md) · Previous: [Command line](command-line.md) · Next: [Glossary →](glossary.md)

What each supported ECU family offers: live parameters, errors, actuator tests and adjustments, with the names the program shows. This page is generated from the definition files by `tools/generate-manual-reference/generate_ecu_reference.py`; do not edit it by hand.

## Families

- [Magneti-Marelli IAW-6F/16F SPI](#magneti-marelli-iaw-6f16f-spi)
- [Magneti-Marelli IAW-8F/18F MPI](#magneti-marelli-iaw-8f18f-mpi)
- [Magneti-Marelli IAW-8F.68 MPI (Alfa 33)](#magneti-marelli-iaw-8f68-mpi-alfa-33)
- [Magneti-Marelli IAW-18FD/1G7 MPI](#magneti-marelli-iaw-18fd1g7-mpi)
- [Weber-Marelli IAW-04K.P8 MPI](#weber-marelli-iaw-04kp8-mpi)
- [FIAT CODE Immobiliser](#fiat-code-immobiliser)

## Magneti-Marelli IAW-6F/16F SPI

- **Technology**: Single-point injection (one injector on the throttle body) for small engines
- **Period**: 1993–2000
- **Vehicles recognised by ISO code**: 18

### Live parameters

| ID | Name | Dashboard group | Shown as |
| --- | --- | --- | --- |
| `PERIODE` | Engine Speed | Engine | round gauge |
| `T_INJ_AP` | Injection Duration | Engine | bar |
| `AVANCE` | Ignition Advance | Engine | bar |
| `MP2_MP8` | Manifold Absolute Pressure | Engine | round gauge |
| `MT_AIR_L` | Air Temperature | Temperatures | bar |
| `MT_EAU_L` | Water Temperature | Temperatures | round gauge |
| `ANG_PAP0` | Throttle Opening Angle | Throttle and idle | bar |
| `M_VBATT` | Battery Voltage | Electrical | round gauge |
| `K_O2` | Lambda Probe Correction | Mixture and lambda | bar |
| `ALFAR` | Idle Stepper Motor Position | Throttle and idle | bar |
| `INTEGR` | Idle Stepper Integral Gain | Throttle and idle | value |
| `PROP` | Idle Stepper Proportional Gain | Throttle and idle | value |
| `TRIMRAM` | Trimmer Position | Self-adaptation | bar |
| `ALFAU` | Autocalibration Correction | Self-adaptation | value |
| `ALFAUC` | Autocalibration Correction with A/C | Self-adaptation | value |
| `CONS_REG` | Idle Engine Speed | Throttle and idle | value |
| `OFNNTR` | Idle Engine Speed Offset | Throttle and idle | value |
| `DELREG` | Stepper Correction (Delta) | Throttle and idle | value |
| `ALFAFTR` | Stepper Correction from FLT | Throttle and idle | value |
| `ADOFFSET` | AC Offset Parameter (min. load, EVAP off) | Self-adaptation | value |
| `ADOFFPURG` | AC Offset Parameter (min. load, EVAP on) | Self-adaptation | value |
| `ADOFFPL` | AC Offset Parameter (partial load) | Self-adaptation | value |
| `ADGAIN` | AC Gain Parameter (med. load) | Self-adaptation | value |
| `S2` | Engine Running | Status | light |
| `S3` | Sensor Signals OK | Status | light |
| `S4` | Throttle at Min/Max | Status | light |
| `S5` | Lambda Sensor in Closed Loop | Status | light |
| `S6` | Air Conditioner ON | Status | light |
| `S7` | Mixture AutoCalibration ENABLED | Status | light |
| `S23` | Stepper AutoCalibration ENABLED | Status | light |
| `S27` | Power Latch Line OK | Status | light |

### Errors

- **Engine** (24): Throttle Position Sensor, MAP Sensor, Lambda Sensor, Water Temperature Sensor, Air Temperature Sensor, Battery Voltage, Idle Regulation, Unknown INPUT ERROR (8), Injector, Ignition Coil 1, Ignition Coil 2, Idle Stepper Motor, EVAP Valve, Air Conditioner, Fuel Pump, Generic System Relay, Autocalibration Parameter, RAM, ROM, EEPROM, CPU, TDC/RPM Sensor, Unknown FUNC ERROR (7), Unknown FUNC ERROR (8)
- **Immobiliser** (8): ECU not synchronized with CODE, ECU Disallowed to Start Engine, ECU has received the 'Universal Code', Unknown CODE ERROR (4), Backdoor Activated, Key Code Error, Incorrect or Unrecognized Code, No Code Received or Link Down

### Actuator tests

| Name | Max duration | Condition |
| --- | --- | --- |
| Fuel Pump Relay | 30 s | ignition on, engine stopped |
| Injector | 5 s | ignition on, engine stopped |
| Coil 1 | 5 s | ignition on, engine stopped |
| Coil 2 | 5 s | ignition on, engine stopped |
| EVAP Valve | 7 s | ignition on, engine stopped |
| REV Meter | 2 s | ignition on, engine stopped |
| AC Relay | 30 s | ignition on, engine stopped |
| Generic Relay | 30 s | ignition on, engine stopped |

Clear codes: max 10 s.

### Adjustments

| Name | Type |
| --- | --- |
| Toggle fuel trim autocalibration | on/off |
| Toggle idle stepper autocalibration | on/off |
| Fuel trim autocalibration reset | on/off |
| Idle stepper autocalibration reset | on/off |
| Set trimmer position (TRIMRAM) | new value |
| Set stepper correction (ALFAFTR) | new value |

## Magneti-Marelli IAW-8F/18F MPI

- **Technology**: Sequential phased multipoint injection (MPI), common on Fire and boxer engines
- **Period**: 1993–1999
- **Vehicles recognised by ISO code**: 7

### Live parameters

| ID | Name | Dashboard group | Shown as |
| --- | --- | --- | --- |
| `PERIODE` | Engine Speed | Engine | round gauge |
| `T_INJ_AP` | Injection Duration | Engine | bar |
| `AVANCE` | Ignition Advance | Engine | bar |
| `MP2_MP8` | Manifold Absolute Pressure | Engine | round gauge |
| `MT_AIR_L` | Air Temperature | Temperatures | bar |
| `MT_EAU_L` | Water Temperature | Temperatures | round gauge |
| `ANG_PAP0` | Throttle Opening Angle | Throttle and idle | bar |
| `M_VBATT` | Battery Voltage | Electrical | round gauge |
| `K_O2` | Lambda Probe Correction | Mixture and lambda | bar |
| `ALFAR` | Idle Stepper Motor Position | Throttle and idle | bar |
| `INTEGR` | Idle Stepper Integral Gain | Throttle and idle | value |
| `PROP` | Idle Stepper Proportional Gain | Throttle and idle | value |
| `TRIMRAM` | Trimmer Position | Self-adaptation | bar |
| `CONS_REG` | Idle Engine Speed | Throttle and idle | value |
| `OFNNTR` | Idle Engine Speed Offset | Throttle and idle | value |
| `S2` | Engine Running | Status | light |
| `S3` | Sensor Signals OK | Status | light |
| `S4` | Throttle at Min/Max | Status | light |
| `S5` | Lambda Sensor in Closed Loop | Status | light |
| `S6` | Air Conditioner ON | Status | light |
| `S7` | Mixture AutoCalibration ENABLED | Status | light |
| `S25` | Ignition Advance Decreased for Knocking | Status | light |
| `S26` | Stepper AutoCalibration ENABLED | Status | light |
| `S27` | Gear Engaged | Status | light |

### Errors

- **Engine** (32): Throttle Position Sensor, MAP Sensor, Lambda Sensor, Water Temperature Sensor, Air Temperature Sensor, Battery Voltage, Idle Regulation, Knocking Sensor, Injectors, Ignition Coil 1, Ignition Coil 2, Idle Stepper Motor, EVAP Valve, Air Conditioner, Fuel Pump, MIL Lamp, Unknown OUTPUT Error (1a), Unknown OUTPUT Error (2a), Unknown OUTPUT Error (3a), Unknown OUTPUT Error (4a), Mixture ratio, Waste Gate, Consumption Meter, EGR Control, Autocalibration Parameter, ROM, RAM, EEPROM, CPU, TDC/RPM Sensor, Automatic Transmission, EGR Valve
- **Immobiliser** (8): ECU not synchronized with CODE, ECU Disallowed to Start Engine, ECU has received the 'Universal Code', Unknown CODE ERROR (4), Backdoor Activated, Key Code Error, Incorrect or Unrecognized Code, No Code Received or Link Down

### Actuator tests

| Name | Max duration | Condition |
| --- | --- | --- |
| Fuel Pump Relay | 30 s | ignition on, engine stopped |
| Injectors | 5 s | ignition on, engine stopped |
| Coil 1 | 5 s | ignition on, engine stopped |
| Coil 2 | 5 s | ignition on, engine stopped |
| EVAP Valve | 7 s | ignition on, engine stopped |
| REV Meter | 2 s | ignition on, engine stopped |
| AC Relay | 30 s | ignition on, engine stopped |
| MIL Lamp | 30 s | ignition on, engine stopped |
| EGR Valve | 30 s | ignition on, engine stopped |
| Consumption Meter | 30 s | ignition on, engine stopped |

Clear codes: max 10 s.

### Adjustments

| Name | Type |
| --- | --- |
| Toggle fuel trim autocalibration | on/off |
| Toggle idle stepper autocalibration | on/off |
| Fuel trim autocalibration reset | on/off |
| Idle stepper autocalibration reset | on/off |

## Magneti-Marelli IAW-8F.68 MPI (Alfa 33)

- **Technology**: Multipoint injection calibrated for the Alfa Romeo boxer engines
- **Period**: 1992–1995
- **Vehicles recognised by ISO code**: 1

### Live parameters

| ID | Name | Dashboard group | Shown as |
| --- | --- | --- | --- |
| `PERIODE` | Engine Speed | Engine | round gauge |
| `T_INJ_AP` | Injection Duration | Engine | bar |
| `AVANCE` | Ignition Advance | Engine | bar |
| `MP2_MP8` | Manifold Absolute Pressure | Engine | round gauge |
| `MT_AIR_L` | Air Temperature | Temperatures | bar |
| `MT_EAU_L` | Water Temperature | Temperatures | round gauge |
| `ANG_PAP0` | Throttle Opening Angle | Throttle and idle | bar |
| `M_VBATT` | Battery Voltage | Electrical | round gauge |
| `K_O2` | Lambda Probe Correction | Mixture and lambda | bar |
| `ALFAR` | Idle Stepper Motor Position | Throttle and idle | bar |
| `INTEGR` | Idle Stepper Integral Gain | Throttle and idle | value |
| `PROP` | Idle Stepper Proportional Gain | Throttle and idle | value |
| `TRIMRAM` | Trimmer Position | Self-adaptation | bar |
| `ALFAU` | Autocalibration Correction | Self-adaptation | value |
| `ALFAUC` | Autocalibration Correction with A/C | Self-adaptation | value |
| `CONS_REG` | Idle Engine Speed | Throttle and idle | value |
| `OFNNTR` | Idle Engine Speed Offset | Throttle and idle | value |
| `DELREG` | Stepper Correction (Delta) | Throttle and idle | value |
| `ALFAFTR` | Stepper Correction from FLT | Throttle and idle | value |
| `ADOFFSET` | AC Offset Parameter (min. load, EVAP off) | Self-adaptation | value |
| `ADOFFPURG` | AC Offset Parameter (min. load, EVAP on) | Self-adaptation | value |
| `ADOFFPL` | AC Offset Parameter (partial load) | Self-adaptation | value |
| `ADGAIN` | AC Gain Parameter (med. load) | Self-adaptation | value |
| `S2` | Engine Running | Status | light |
| `S3` | Sensor Signals OK | Status | light |
| `S4` | Throttle at Min/Max | Status | light |
| `S5` | Lambda Sensor in Closed Loop | Status | light |
| `S6` | Air Conditioner ON | Status | light |
| `S7` | Mixture AutoCalibration ENABLED | Status | light |

### Errors

- **Engine** (24): Throttle Position Sensor, MAP Sensor, Lambda Sensor, Water Temperature Sensor, Air Temperature Sensor, Battery Voltage, Idle Regulation, Unknown INPUT ERROR (8), Injector, Ignition Coil 1, Ignition Coil 2, Idle Stepper Motor, EVAP Valve, Air Conditioner, Fuel Pump, Generic System Relay, Autocalibration Parameter, RAM, ROM, EEPROM, CPU, TDC/RPM Sensor, Unknown FUNC ERROR (7), Unknown FUNC ERROR (8)

### Actuator tests

| Name | Max duration | Condition |
| --- | --- | --- |
| Fuel Pump Relay | 30 s | ignition on, engine stopped |
| Injectors | 5 s | ignition on, engine stopped |
| Coil 1 | 5 s | ignition on, engine stopped |
| Coil 2 | 5 s | ignition on, engine stopped |
| EVAP Valve | 7 s | ignition on, engine stopped |
| REV Meter | 2 s | ignition on, engine stopped |
| AC Relay | 30 s | ignition on, engine stopped |
| Generic Relay | 30 s | ignition on, engine stopped |

Clear codes: max 10 s.

### Adjustments

| Name | Type |
| --- | --- |
| Toggle fuel trim autocalibration | on/off |
| Toggle idle stepper autocalibration | on/off |
| Fuel trim autocalibration reset | on/off |
| Idle stepper autocalibration reset | on/off |

## Magneti-Marelli IAW-18FD/1G7 MPI

- **Technology**: Multipoint electronic injection (MPI) with static ignition
- **Period**: 1995–2001
- **Vehicles recognised by ISO code**: 3

### Live parameters

| ID | Name | Dashboard group | Shown as |
| --- | --- | --- | --- |
| `PERIODE` | Engine Speed | Engine | round gauge |
| `T_INJ_AP` | Injection Duration | Engine | bar |
| `AVANCE` | Ignition Advance | Engine | bar |
| `MP2_MP8` | Manifold Absolute Pressure | Engine | round gauge |
| `MT_AIR_L` | Air Temperature | Temperatures | bar |
| `MT_EAU_L` | Water Temperature | Temperatures | round gauge |
| `ANG_PAP0` | Throttle Opening Angle | Throttle and idle | bar |
| `M_VBATT` | Battery Voltage | Electrical | round gauge |
| `K_O2` | Lambda Probe Correction | Mixture and lambda | bar |
| `K_O2_RAW` | Lambda Probe Voltage | Mixture and lambda | bar |
| `ALFAR` | Idle Stepper Motor Position | Throttle and idle | bar |
| `INTEGR` | Idle Stepper Integral Gain | Throttle and idle | value |
| `PROP` | Idle Stepper Proportional Gain | Throttle and idle | value |
| `TRIMRAM` | Trimmer Position | Self-adaptation | bar |
| `ALFAU` | Autocalibration Correction | Self-adaptation | value |
| `ALFAUC` | Autocalibration Correction with A/C | Self-adaptation | value |
| `CONS_REG` | Idle Engine Speed | Throttle and idle | value |
| `OFNNTR` | Idle Engine Speed Offset | Throttle and idle | value |
| `OFFSET1` | Autocalibration Injection Offset @ ZONE 1 | Self-adaptation | bar |
| `OFFSET2` | Autocalibration Injection Offset @ ZONE 2 | Self-adaptation | bar |
| `OFFSET3` | Autocalibration Injection Offset @ ZONE 3 | Self-adaptation | bar |
| `OFFSET4` | Autocalibration Injection Offset @ ZONE 4 | Self-adaptation | bar |
| `OFFSET5` | Autocalibration Injection Offset @ ZONE 5 | Self-adaptation | bar |
| `OFFSET6` | Autocalibration Injection Offset @ ZONE 6 | Self-adaptation | bar |
| `OFFSETm` | Autocalibration Injection Offset @ IDLE | Self-adaptation | bar |
| `S2` | Engine Running | Status | light |
| `S3` | Sensor Signals OK | Status | light |
| `S4` | Throttle at Min/Max | Status | light |
| `S5` | Lambda Sensor in Closed Loop | Status | light |
| `S6` | Air Conditioner ON | Status | light |
| `S7` | Mixture AutoCalibration ENABLED | Status | light |
| `S25` | Ignition Advance Decreased for Knocking | Status | light |
| `S26` | Stepper AutoCalibration ENABLED | Status | light |
| `S27` | Gear Engaged | Status | light |

### Errors

- **Engine** (32): Throttle Position Sensor, Unknown INPUT ERROR (2), Lambda Sensor, Water Temperature Sensor, Air Temperature Sensor, Battery Voltage, Idle Regulation, Knocking Sensor, Injectors (group 1), Ignition Coil 1, Ignition Coil 2, Idle Stepper Motor, EVAP Valve, Air Conditioner, Fuel Pump, MIL Lamp, Unknown OUTPUT Error (1a), Unknown OUTPUT Error (2a), Unknown OUTPUT Error (3a), Injectors (group 2), Mixture ratio, Waste Gate, Consumption Meter, MAP Sensor, Autocalibration Parameter, RAM, ROM, EEPROM, CPU, TDC/RPM Sensor, Automatic Transmission, EGR Valve
- **Immobiliser** (8): ECU not synchronized with CODE, ECU Disallowed to Start Engine, ECU has received the 'Universal Code', Unknown CODE ERROR (4), Backdoor Activated, Key Code Error, Incorrect or Unrecognized Code, No Code Received or Link Down

### Actuator tests

| Name | Max duration | Condition |
| --- | --- | --- |
| Fuel Pump Relay | 30 s | ignition on, engine stopped |
| Injectors | 10 s | ignition on, engine stopped |
| Coil 1 | 5 s | ignition on, engine stopped |
| Coil 2 | 5 s | ignition on, engine stopped |
| EVAP Valve | 7 s | ignition on, engine stopped |
| REV Meter | 2 s | ignition on, engine stopped |
| AC Relay | 30 s | ignition on, engine stopped |
| MIL Lamp | 30 s | ignition on, engine stopped |
| EGR Valve | 30 s | ignition on, engine stopped |
| Consumption Meter | 30 s | ignition on, engine stopped |

Clear codes: max 10 s.

### Adjustments

| Name | Type |
| --- | --- |
| Toggle fuel trim autocalibration | on/off |
| Toggle idle stepper autocalibration | on/off |
| Fuel trim autocalibration reset | on/off |
| Idle stepper autocalibration reset | on/off |

## Weber-Marelli IAW-04K.P8 MPI

- **Technology**: High-performance ECU for the turbocharged and multi-valve engines of 1990s sports cars
- **Period**: 1989–1996
- **Also known as**: IAW 4WF.P8
- **Vehicles recognised by ISO code**: 17

### Live parameters

| ID | Name | Dashboard group | Shown as |
| --- | --- | --- | --- |
| `PERIODE` | Engine Speed | Engine | round gauge |
| `T_INJ_AP` | Injection Duration | Engine | bar |
| `AVANCE` | Ignition Advance | Engine | bar |
| `MP2_MP8` | Manifold Absolute Pressure | Engine | round gauge |
| `MP2_MP8t` | Manifold Absolute Pressure (TURBO) | Engine | round gauge |
| `MT_AIR_L` | Air Temperature | Temperatures | bar |
| `MT_EAU_L` | Water Temperature | Temperatures | round gauge |
| `ANG_PAP0` | Throttle Opening Angle | Throttle and idle | bar |
| `M_VBATT` | Battery Voltage | Electrical | round gauge |
| `ALFAR` | Idle Stepper Motor Position | Throttle and idle | bar |
| `TRIMRAM` | Trimmer Position | Self-adaptation | bar |
| `INJ_AVA` | Injection Timing Angle | Engine | value |
| `T_VAE` | VAE Duty Cycle (Idle Control) | Throttle and idle | bar |
| `K_O2` | Lambda Probe Correction | Mixture and lambda | bar |
| `FLGSMA` | Lambda in Closed Loop | Status | light |
| `FABEPAT` | Self-Adaptation Activated in EPROM | Status | light |
| `FABEEAT` | Self-Adaptation Activated in EEPROM | Status | light |
| `FABEPATi` | Idle Self-Adaptation Activated in EPROM | Status | light |
| `FABEEATi` | Idle Self-Adaptation Activated in EEPROM | Status | light |
| `UR2` | Ignition Advance Decreased for Knocking | Status | light |
| `UR3` | Signal panel OK | Status | light |
| `UR4` | Throttle at Min/Max | Status | light |
| `UR5` | ABS active | Status | light |
| `UR6` | Air Conditioner ON | Status | light |
| `UR7` | Gear Engaged | Status | light |
| `UR8` | VAE Self Cleaning underway | Status | light |

### Errors

- **Engine** (16): VAE blocked open or closed, Mixture ratio, Air Temperature Sensor, Knocking sensor, Water Temperature Sensor, Lambda Sensor, MAP Sensor, Throttle Position Sensor, Cam sensor, Signal Panel Sequence, EPROM, EEPROM, Neutral/drive signal, Digimatic signal, RAM, SMOT sensor

### Actuator tests

| Name | Max duration | Condition |
| --- | --- | --- |
| Fuel Pump Relay | 30 s | ignition on, engine stopped |
| Injectors | 5 s | ignition on, engine stopped |
| Injector 1 | 5 s | ignition on, engine stopped |
| Injector 2 | 5 s | ignition on, engine stopped |
| Injector 3 | 5 s | ignition on, engine stopped |
| Injector 4 | 5 s | ignition on, engine stopped |
| Coil 1 | 5 s | ignition on, engine stopped |
| Coil 2 | 5 s | ignition on, engine stopped |
| Idle Control Valve (VAE) | 5 s | ignition on, engine stopped |
| Over Boost | 5 s | ignition on, engine stopped |
| EVAP Valve | 5 s | ignition on, engine stopped |
| REV Meter | 2 s | ignition on, engine stopped |
| Consumption Meter | 10 s | ignition on, engine stopped |

Clear codes: max 10 s.

### Adjustments

None.

## FIAT CODE Immobiliser

- **Technology**: Immobiliser: does not control the injection; it talks to the transponder in the key to enable starting
- **Period**: 1995–
- **Vehicles recognised by ISO code**: 43

### Live parameters

| ID | Name | Dashboard group | Shown as |
| --- | --- | --- | --- |
| `CRDVAS` | Error Counter | Other | value |

### Errors

- **Immobiliser** (8): ECU not synchronized with CODE, ECU Disallowed to Start Engine, ECU has received the 'Universal Code', Unknown CODE ERROR (4), Backdoor Activated, Key Code Error, Incorrect or Unrecognized Code, No Code Received or Link Down

### Actuator tests

None.

Clear codes: max 10 s.

### Adjustments

None.
