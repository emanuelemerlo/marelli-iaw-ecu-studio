# Installation and first connection

[← Manual](README.md) · Next: [The main window →](main-window.md)

## What you need

- A computer with Windows (x64), Linux (x64, or arm64 such as a Raspberry Pi) or macOS (Apple silicon).
- To talk to a car: a **KKL 409.1 USB cable** with a genuine FTDI chip and an **OBD2 → FIAT 3-pin adapter**. ELM327 adapters cannot work with these ECUs. All the details are in [Hardware](../../hardware.md).
- Nothing at all to try the program: the built-in **simulator** behaves like a car.

## Installing

1. Open the [releases page](https://github.com/emanuelemerlo/marelli-iaw-ecu-studio/releases) and download the archive for your system:

   | System | File |
   | --- | --- |
   | Windows x64 | `…-win-x64.zip` |
   | Linux x64 | `…-linux-x64.tar.gz` |
   | Raspberry Pi / Linux arm64 | `…-linux-arm64.tar.gz` |
   | macOS Apple silicon | `…-osx-arm64.tar.gz` |

2. Extract it into a folder of your choice. It contains two programs, and needs no installation and no .NET:
   - `marelli-iaw-ecu-studio` — the desktop application described in this manual;
   - `iaw-cli` — the [command line tool](command-line.md).
3. Start `marelli-iaw-ecu-studio`.

The programs are not signed, so the system warns the first time:

- **Windows**: SmartScreen shows "Windows protected your PC": click *More info* → *Run anyway*.
- **macOS**: right-click the program → *Open*, or in a terminal run `xattr -d com.apple.quarantine marelli-iaw-ecu-studio iaw-cli`.

To check the download, compare its checksum with `SHA256SUMS.txt` on the release page.

### Before the first connection with the cable

- **Windows**: install the FTDI VCP driver if Windows does not do it by itself. The cable appears as a `COM` port.
- **Linux**: add yourself to the `dialout` group (`sudo usermod -aG dialout $USER`), then log out and in again.
- **Everywhere**: set the FTDI latency timer to 1 ms for faster readings. The program reminds you with a yellow hint under the toolbar when the latency is too high. How to do it: [Hardware](../../hardware.md).

## First start: try the simulator

The window opens maximised the first time; afterwards it opens as you left it.

1. In **Interface**, choose **Simulator — demo, no cable needed**.
2. In **ECU**, leave **Automatic Detection**, or pick a family to simulate (for example *Weber-Marelli IAW-04K.P8 MPI* to see a turbo car).
3. Click **Connect** (or press **F10**). After about three seconds the ECU is recognised, a **Simulation** badge appears in the ECU bar and the dashboard comes alive.

Everything works in the simulator, including errors, actuator tests and recording, so it is the safest place to learn the program. Click **Disconnect** when you are done.

## Connecting to the car

1. With the **ignition off**, plug the 3-pin adapter into the FIAT diagnostic connector (its position varies by model: often in the engine bay near the ECU, or behind the dashboard) and the KKL cable into the computer.
2. In **Interface**, choose the cable's port (`COM3`, `/dev/ttyUSB0`…). If it is not in the list, click **Refresh**.
3. In **ECU**, leave **Automatic Detection**. If you know the code on the ECU label (for example *IAW 4WF.P8*) you can type it in the search box to choose the family directly; see [The main window](main-window.md#choosing-the-ecu).
4. Click **Connect**. The status bar says *Please turn ON the ignition...*
5. **Now turn the key to MAR (ignition on).** Do not start the engine yet.

Like IES2, the program first listens up to 10 seconds for the ISO code that some ECUs send by themselves at key-on; if nothing arrives it wakes the ECU up and asks for the code. When the ECU answers:

- the ECU bar shows the ECU name, ISO code, CODRIC, vehicle and immobiliser;
- the [Dashboard](dashboard.md) and the other tabs fill with live data;
- the status bar shows *Connected* and the refresh rate (for example *6.8 updates/s*).

If the ignition is already on when you click Connect, untick **Wait for the ISO code at key-on** in [Options](options.md) to skip the 10-second wait.

To stop, click **Disconnect** and only then turn the ignition off.

### If it does not connect

- **Cancel** (or **Esc**) stops a connection attempt.
- *ECU could not be recognized* means the ECU answered but its ISO code is not in any definition: choose the family by hand in **ECU** and try again.
- *An error has occurred while opening the port* means the port is in use, missing, or you lack permission (Linux: `dialout` group).

More in [Troubleshooting](troubleshooting.md).

## What next

- Read the engine data on the [Dashboard](dashboard.md) and in [Parameters](parameters.md).
- Check the [Errors](errors.md).
- Before any actuator test, read [Actuator tests and adjustments](tests-and-adjustments.md).
