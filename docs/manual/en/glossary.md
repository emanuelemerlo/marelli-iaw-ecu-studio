# Glossary

[← Manual](README.md) · Previous: [ECU reference](ecu-reference.md)

| Term | Meaning |
| --- | --- |
| **Active command** | A request that makes the ECU do something (actuator test, clear codes, adjustment) instead of just answering with a value. The ECU accepts it only in active mode. |
| **Actuator** | A component the ECU drives: injector, ignition coil, fuel pump relay, idle stepper, canister valve… |
| **Canister (EVAP)** | Activated-carbon canister that stores fuel vapours from the tank. The ECU purges it into the intake through the canister valve; some self-adaptation values differ with the canister closed or open. |
| **Closed loop** | The ECU corrects the mixture using the lambda probe. In open loop (cold engine, full load) it uses its maps only. |
| **CODRIC** | Hardware code of the ECU. Used to recognise the family and to check that the same unit answers after a reconnection. |
| **Compatibility mode** | Slower timing for adapters that lose bytes, inherited from IES2. |
| **CO trimmer (TRIMRAM)** | Adjustment of the idle mixture (CO) stored in the ECU, set in steps. On older cars it was a screw on the air flow meter. |
| **Duty cycle** | Percentage of time a valve is open, for example the VAE idle valve. |
| **ECOL** | Suffix shown after the ECU name when the car has no immobiliser, as in IES2. |
| **Echo** | The KKL cable sends back every byte it transmits. The program uses the echo to tell a faulty cable from an ECU that does not answer. |
| **EGR** | Exhaust gas recirculation valve. |
| **EOBD / OBD-II** | Standard on-board diagnostics of cars from about 2001, on the 16-pin connector. The 1990s IAW ECUs predate it (OBD-I). |
| **FIAT 3-pin connector** | Diagnostic connector of these cars: +12 V, ground, K-Line. |
| **FIAT CODE** | The immobiliser of FIAT, Lancia and Alfa Romeo cars: the ECU starts the engine only after the key code has been recognised. |
| **FTDI latency timer** | How long the USB chip of the cable waits before passing data to the computer. 1 ms makes readings much faster than the default 16 ms. |
| **IAW** | *Iniezione Accensione Weber*: the Magneti Marelli (ex Weber) family of injection and ignition ECUs. |
| **IES2** | *IAW Scan 2*, the original Windows program by Tomasz Orczyk that this program continues. |
| **ISO code** | Identification code of the ECU software and calibration (six bytes, shown as `55-D0-85-02-94-40`). It decides the family and the vehicle. Some ECUs send it by themselves at key-on. |
| **K-Line** | The single wire over which the ECU and the cable talk. |
| **KKL cable** | USB to K-Line cable ("VAG-COM KKL 409.1"), the interface this program needs. |
| **Knocking** | Detonation in the cylinders. The ECU reduces the ignition advance when it detects it (amber light on the dashboard). |
| **Lambda probe** | Oxygen sensor in the exhaust. The *lambda correction* is how much the ECU changes the injection to keep the mixture right (1 = no correction). |
| **Line trace** | Text file with every byte sent and received on the K-Line, with timestamps. Essential for reporting communication problems. |
| **MAP** | Manifold absolute pressure, in hPa: the engine load. About 1000 hPa with the engine off, much lower at idle. |
| **MAR** | Position of the ignition key with the ignition on (*marcia*). *STOP* is off, *AVV* is crank. |
| **MIL** | Malfunction indicator lamp, the check-engine light. |
| **MPI / SPI** | Multi-point injection (one injector per cylinder) / single-point injection (one injector on the throttle body). |
| **RFC 2217** | Network protocol that carries a serial port, including speed changes, over TCP. Used by the network bridge. |
| **Self-adaptation** (*autoadattivo*) | What the ECU learns over time to compensate for wear and tolerances: mixture offsets per load zone, idle position. It can be switched off and reset in [Adjustments](tests-and-adjustments.md#adjustments). Called *autocalibration* (AC) in IES2. |
| **Stepper** | Idle control motor: it opens a bypass around the throttle in steps to keep the target idle speed. |
| **Target idle speed** | The idle speed the ECU aims for (it rises with the engine cold or the A/C on). |
| **VAE** | Idle control valve of the IAW-04K.P8, driven with a duty cycle. |
| **Verified** | A definition marked as checked. Only verified definitions run active commands. |
