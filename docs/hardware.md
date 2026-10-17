# Hardware

## What you need

1. **A KKL cable** ("VAG-COM KKL 409.1", USB). It must be a plain K-Line cable: avoid "K+DCAN" or switchable models. Prefer a seller who states a **genuine FTDI FT232R** chip; cloned chips are common and some were disabled by FTDI's Windows driver in the past. CH340 based cables may work but are less tested.
2. **An OBD2 to FIAT 3-pin adapter.** The 3-pin connector carries +12 V, ground and K-Line. Before the first connection, check with a multimeter that each signal reaches the right OBD2 pin (16 = +12 V, 4/5 = ground, 7 = K-Line): cheap adapters are not always wired correctly.

The ECU only talks with the ignition on. Like IES2, the software first listens for 10 seconds for an ISO code that the ECU may send by itself at key-on, so start the software first, then turn the key. If nothing arrives it initialises the ECU and asks for the code. To skip the wait when the ignition is already on, untick "Wait for the ISO code at key-on" in the application's Options, or pass `--no-broadcast` to `iaw-cli`.

## The protocol, briefly

These ECUs predate OBD-II. After an init sequence at 1200 baud (bytes `0F AA CC`), the ECU answers at **7680 baud**, one byte for each one-byte request. The KKL cable echoes every sent byte. Requests from `0x80` up are active commands (actuator tests, clear codes, adjustments) and are accepted only in active mode (`0xAA` to enter, `0xFF` to leave). IAW-04K.P8 needs no init.

This is why ELM327 adapters do not work: they only send framed OBD messages at standard speeds.

## Windows

Install the FTDI VCP driver if Windows does not do it. The port appears as `COMn` (`iaw-cli ports`).

For faster polling, set the latency timer to 1 ms: Device Manager → Ports → USB Serial Port → Properties → Port Settings → Advanced → Latency Timer.

## Linux (including Raspberry Pi)

The cable appears as `/dev/ttyUSB0`; `iaw-cli ports` also shows a stable `/dev/serial/by-id/...` name that does not change when you replug it.

Permissions: add yourself to the `dialout` group, then log out and in again.

```sh
sudo usermod -aG dialout $USER
```

Latency: the FTDI driver waits 16 ms by default before passing data on, which slows down a protocol of one byte per request. `iaw-cli ports` warns when this is the case. To set 1 ms for every FTDI adapter, create `/etc/udev/rules.d/99-ftdi-latency.rules`:

```
ACTION=="add", SUBSYSTEM=="usb-serial", DRIVER=="ftdi_sio", ATTR{latency_timer}="1"
```

then run `sudo udevadm control --reload` and replug the cable.

7680 baud is not a standard POSIX speed. .NET sets it through `termios2`; the `ftdi_sio` driver supports it. If a cable rejects it, `iaw-cli` reports the error instead of communicating at a wrong speed.

## macOS

Use the `/dev/cu.*` device of the cable. Recent macOS versions include a driver for FTDI chips. Custom speeds are set with `IOSSIOSPEED`; support depends on the driver and has not been tested yet.

## Compatibility notes

| Adapter | Status |
| --- | --- |
| KKL 409.1, genuine FTDI FT232R | Reference adapter, to be verified on the bench |
| KKL, CH340 | Not tested |
| KKL, PL2303 clones | Not tested; often reject custom speeds |
| ELM327 (any interface) | Not possible (protocol) |
| Bluetooth KKL (transparent SPP) | Not supported: the 1200→7680 baud change does not reach the cable |

Please report results with your adapter by opening an issue, including the output of `iaw-cli ports`.

## Reporting a problem: the line trace

A line trace records every byte sent and received on the K-Line, with timestamps, line speed and notes such as "connect" or "reconnect attempt". It shows at once whether the problem is the cable (no echo), the line (wrong echo) or the ECU (echo but no answer), so please attach one to every issue about communication.

- Desktop application: Options → "Save a line trace of each connection". The file `iaw_trace_<date>.txt` goes into the log folder; its path is shown in the status bar.
- Command line: add `--trace file.txt` to any command, for example `iaw-cli identify --port /dev/ttyUSB0 --trace identify.txt`.

`iaw-cli trace file.txt` prints a summary: requests, timeouts, wrong echoes, and the timeline of notes and timeouts. The file is plain text (`time;event;baud;byte;note`), see `LineTraceWriter` for the format.

A trace can also stand in for the car. `iaw-cli read --replay file.txt` answers every request with the recorded bytes: in the default `strict` mode the software must send the same requests in the same order (a regression test: it stops and says where it diverged), while `--replay-mode lookup` answers any request that appears in the trace, to try other parameters with the values of a real car.

## Lost communication

When more than one request in a row fails, the session reports why:

| Message | Likely cause |
| --- | --- |
| nothing came back, not even the echo | cable unplugged, USB port, adapter without echo (try `--no-echo`) |
| the adapter echoed a different byte | noise on the line, wrong speed, adapter without echo; try compatibility mode |
| the adapter echoes but the ECU does not answer | ignition off, 3-pin connector, K-Line wire |

The application and `iaw-cli read` then try to reconnect by themselves (after 0.5, 1, 2, then every 5 seconds): they repeat the init and check that the ECU answering has the same CODRIC, so a different unit is never mistaken for the one connected. Turn the ignition off and on again if the ECU does not come back. Untick "Reconnect automatically" in Options, or pass `--no-reconnect`, to stop at the first loss as IES2 did; `--reconnect-attempts n` limits the attempts.
