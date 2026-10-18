# Command line (`iaw-cli`)

[← Manual](README.md) · Previous: [Troubleshooting](troubleshooting.md) · Next: [ECU reference →](ecu-reference.md)

`iaw-cli` does from a terminal what the application does: identify, read, errors, tests. It is handy on a Raspberry Pi without a screen, for long recordings, for scripts and for problem reports. It is in the same archive as the application.

```sh
iaw-cli <command> [options]
iaw-cli help
```

On Windows run it as `iaw-cli.exe` from a Command Prompt or PowerShell in the program folder; on Linux and macOS as `./iaw-cli`.

**It is read-only by default**: tests and clear codes need `--allow-active`, and ask for confirmation.

## Commands

| Command | What it does |
| --- | --- |
| `ecus` | Lists the known ECU families with their ISO codes, other names, technology, period and compatible vehicles. |
| `ports` | Lists the serial ports: USB chip, stable name (`/dev/serial/by-id/…` on Linux), FTDI latency. |
| `identify` | Connects and shows ECU family, ISO code, CODRIC, vehicle and immobiliser. |
| `read` | Reads live parameters, once or continuously, optionally to a CSV file. |
| `errors` | Reads engine and immobiliser errors. |
| `test <name\|number>` | Runs an actuator test (needs `--allow-active`). Without a name or number it lists the tests of the connected ECU. |
| `clear-codes` | Clears stored errors (needs `--allow-active`). |
| `validate <file\|folder>` | Checks ECU definition files. |
| `trace <file>` | Summarises a line trace written with `--trace`. |
| `bridge` | Shares the cable (or the simulator) on the network with RFC 2217. |
| `obd <identify\|read\|dtc\|clear>` | OBD-II / EOBD for cars of 2001 and later, see [below](#obd-ii-cars-2001-and-later). |

## Connection options

| Option | Meaning |
| --- | --- |
| `--port <name>` | Serial port (`COM3`, `/dev/ttyUSB0`) or network bridge (`rfc2217://192.168.4.1:2217`). |
| `--simulate [ecu]` | Use the built-in simulator, optionally of a given family. |
| `--replay <trace>` | Answer with the bytes of a line trace instead of a cable. |
| `--replay-mode <strict\|lookup>` | `strict` (default): same requests in the same order; `lookup`: any request, answered with the recorded values. |
| `--ecu <id\|code\|auto>` | ECU family or label code, for example `IAW-04K.P8` or `4WF.P8` (default: `auto`). |
| `--compat` | Compatibility mode: slower timing for unreliable adapters. |
| `--no-echo` | The adapter does not echo the sent bytes. |
| `--no-broadcast` | Do not wait 10 s for the ISO code at key-on (ignition already on). |
| `--dtr` | Raise DTR. |
| `--definitions <folder>` | Extra definition files, which override the built-in ones. |
| `--lang <code>` | Language of names: `en`, `it`, `de`, `es`, `pl`, `pt`, `nl`, `hu`, `hr`, `sr`. |
| `--trace <file>` | Write every byte on the line to a trace file. |

## `read` options

| Option | Meaning |
| --- | --- |
| `--params <id,id\|all>` | Parameters to read, by ID (see the [ECU reference](ecu-reference.md)); default `all`. |
| `--count <n>` | Number of cycles; `0` = until Ctrl+C. Default 1. |
| `--csv <file>` | Also write a CSV log (same format as the application, can be opened in [Log replay](log-replay.md)). |
| `--no-reconnect` | Stop when communication is lost instead of reconnecting. |
| `--reconnect-attempts <n>` | Give up after n attempts (default 0: keep trying). |
| `--raw` | Also print the raw value of each parameter. |

## Safety options

| Option | Meaning |
| --- | --- |
| `--allow-active` | Allow actuator tests and clear codes. |
| `--allow-unverified` | Also for definitions not marked verified. |
| `--yes` | Do not ask for confirmation. |

## Examples

```sh
# Try without a car
iaw-cli read --simulate --count 5
iaw-cli read --simulate IAW-04K.P8 --lang it --count 5

# Find the cable
iaw-cli ports

# Identify: start the command, then turn the ignition on
iaw-cli identify --port /dev/ttyUSB0
# Ignition already on
iaw-cli identify --port COM3 --no-broadcast

# Read engine speed and coolant temperature until Ctrl+C, and save a CSV
iaw-cli read --port /dev/ttyUSB0 --params PERIODE,MT_EAU_L --count 0 --csv drive.csv

# Errors, in Italian
iaw-cli errors --port /dev/ttyUSB0 --lang it

# Actuator tests: list them with their numbers, then run one (ignition on, engine stopped)
iaw-cli test --port /dev/ttyUSB0
iaw-cli test 1 --port /dev/ttyUSB0 --allow-active
iaw-cli clear-codes --port /dev/ttyUSB0 --allow-active
```

## Line traces

Add `--trace file.txt` to any command to record the session byte by byte. Then:

```sh
iaw-cli trace punto.trace.txt
```

prints requests, timeouts, wrong echoes and the timeline of events: the first thing to look at when communication fails.

### Replaying a line trace

A trace can stand in for the car:

```sh
iaw-cli read --replay punto.trace.txt --count 20
iaw-cli read --replay punto.trace.txt --replay-mode lookup --params all
```

In `strict` mode the program must send exactly the recorded requests, and stops saying where it diverged: a regression test. In `lookup` mode any request that appears in the trace is answered, to try other parameters with the values of a real car.

## Network bridge

```sh
# On the computer with the cable (e.g. a Raspberry Pi in the car)
iaw-cli bridge --port /dev/ttyUSB0 --listen 0.0.0.0:2217
# Try the setup with the simulator
iaw-cli bridge --simulate --listen 0.0.0.0:2217

# On the other computer
iaw-cli read --port rfc2217://192.168.4.1:2217
```

`--listen` defaults to `127.0.0.1:2217` (this computer only). Use `0.0.0.0` on a private network only.

## OBD-II cars (2001 and later)

From about 2001 the petrol FIAT, Lancia and Alfa Romeo cars sold in Europe have EOBD on the 16-pin connector. The same KKL cable works, **without** the 3-pin adapter. This is available on the command line only for now.

```sh
iaw-cli obd identify --port /dev/ttyUSB0      # protocol, ECUs, supported PIDs, VIN
iaw-cli obd read --port /dev/ttyUSB0 --params RPM,ECT,MAP,STFT1 --count 0
iaw-cli obd dtc --port /dev/ttyUSB0           # MIL, stored and pending codes
iaw-cli obd clear --port /dev/ttyUSB0 --allow-active
iaw-cli obd read --simulate kwp-fast          # without a car
```

| Option | Meaning |
| --- | --- |
| `--protocol <auto\|iso9141\|kwp-slow\|kwp-fast>` | K-Line protocol; `auto` tries the 5-baud init, then the KWP2000 fast init. |
| `--simulate [iso9141\|kwp-slow\|kwp-fast]` | Simulated OBD car. |
| `--params <id,id\|all>` | PIDs by name (`RPM`, `ECT`, `MAP`) or number (`0C`, `05`); default all supported. |
| `--count <n>` | Number of cycles, `0` = until Ctrl+C. |

These are the standard EOBD values; manufacturer data (adaptations, actuator tests of each ECU) is not included. CAN cars (about 2008 on) need another adapter and are not supported.
