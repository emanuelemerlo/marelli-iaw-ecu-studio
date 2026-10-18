# Troubleshooting

[← Manual](README.md) · Previous: [Options](options.md) · Next: [Command line →](command-line.md)

## Before anything else

- Try the **Simulator**: if it works, the program is fine and the problem is in the cable, the adapter or the car.
- Run `iaw-cli ports`: it lists the serial ports with their USB chip and stable name, and warns when the FTDI latency is too high.
- Check the 3-pin adapter with a multimeter: OBD2 pin 16 = +12 V, pins 4/5 = ground, pin 7 = K-Line ([Hardware](../../hardware.md)).
- The ECU only talks with the ignition **on**.

## Messages

| Message | Likely cause and what to do |
| --- | --- |
| **The cable is not in the Interface list** | Click **Refresh**. Windows: install the FTDI driver. Linux: check `ls /dev/ttyUSB*`. |
| *An error has occurred while opening the … port.* | The port is used by another program (close IES2, other diagnostics software, serial monitors), it was unplugged, or you lack permission (Linux: `sudo usermod -aG dialout $USER`, then log out and in). |
| Yellow hint *FTDI latency timer is 16 ms…* | Works, but slowly. Set the latency to 1 ms ([Hardware](../../hardware.md)). |
| Stays on *Please turn ON the ignition...* | The program is waiting for the ISO code at key-on: turn the key now. If the ignition was already on, wait 10 seconds or untick **Wait for the ISO code at key-on** in [Options](options.md). |
| *ECU could not be recognized, please turn OFF the ignition...* (ISO …) | The ECU answered with an ISO code no definition knows. Turn the ignition off, choose the family by hand in **ECU** (search the code on the label), and connect again. Please report the ISO code shown. |
| **ISO code … is not in the vehicle table** | Not an error: the ECU is recognised, only the car's name is unknown. Reporting the car and the code helps complete the table. |
| *No ECU Response — nothing comes back from the adapter, not even the echo…* | Cable unplugged, USB port, or an adapter that does not echo. Check the USB connection; try **Raise DTR**. |
| *No ECU Response — the adapter echoes a different byte…* | Noise on the line or wrong speed. Try **Compatibility mode**; check the adapter wiring; try another USB port or a shorter USB cable. |
| *No ECU Response — the adapter works but the ECU does not answer…* | Ignition off, 3-pin connector loose or wired wrongly, broken K-Line wire. |
| *A different ECU answers (its CODRIC changed)* | After reconnecting, another unit answered (another car, or a swapped ECU). Disconnect and connect again. |
| *Active commands are disabled in the options.* | Tick **Enable active commands** in [Options](options.md). |
| A test shows **INIT. FAIL** | The ECU refused active mode. Check the condition (ignition on, engine stopped), then disconnect, cycle the ignition and try again. |
| A value shows **#ERR** | The ECU sent a value that cannot be decoded. Record a CSV log and a line trace and report it. |
| *The file could not be read* (Log replay) | Not a CSV log from this program or from IES2, or the file is damaged. |
| macOS: *cannot be opened because the developer cannot be verified* | Right-click → *Open*, or `xattr -d com.apple.quarantine marelli-iaw-ecu-studio`. |

## Lost communication

When more than one request in a row gets no valid answer, the status bar shows *No ECU Response* with the likely cause (see the table above) and the **Reconnect** button appears.

With **Reconnect automatically** on (the default), the program repeats the initialisation after 0.5, 1 and 2 seconds and then every 5 seconds, showing *reconnecting, attempt n...*. Each time it checks the CODRIC, so a different ECU is never mistaken for the one connected. When the ECU answers again, the status bar says *Connected: … (reconnected)* and the readings go on, recording included.

If it does not come back: turn the ignition off and on again, check the connector, or press **Reconnect** (F12) to retry at once. **Disconnect** gives up.

Typical causes: ignition turned off, the starter motor pulling the battery voltage down during cranking, a loose connector, the computer going to sleep.

## Reporting a problem

The most useful thing is a **line trace**: it records every byte on the K-Line and shows at once whether the problem is the cable, the line or the ECU.

1. In [Options](options.md), tick **Save a line trace of each connection**.
2. Connect and reproduce the problem.
3. Disconnect. The status bar shows the path of `iaw_trace_<date>_<time>.txt`.
4. Open a [real car report](https://github.com/emanuelemerlo/marelli-iaw-ecu-studio/issues/new?template=car_report.yml) and attach the trace, plus the CSV log if a value looks wrong. Include car, engine, the code on the ECU label, cable, operating system and what happened.

Reports are welcome even when everything works, and can be written in Italian. For general help see [SUPPORT.md](../../../SUPPORT.md).

`iaw-cli trace <file>` prints a summary of a trace: requests, timeouts, wrong echoes, and the timeline.
