# Options

[← Manual](README.md) · Previous: [Log replay](log-replay.md) · Next: [Troubleshooting →](troubleshooting.md)

Click **Options** at the top right. Every change is saved at once and kept for the next start. The options that affect the connection apply from the next **Connect**.

| Option | Default | What it does |
| --- | --- | --- |
| **Language** | English | Language of the interface and of parameter, error and message names: English, Deutsch, Español, Hrvatski, Magyar, Italiano, Nederlands, Polski, Português, Srpski. Menus and buttons exist in English and Italian; in the other languages they stay in English and only the names coming from the ECU are translated. Changes take effect at once, even while connected. |
| **Theme** | Same as the system | *Same as the system*, *Light* or *Dark*. Also from the theme button next to Options. |
| **Wait for the ISO code at key-on** | on | Before initialising, listen up to 10 seconds for the ISO code that some ECUs send by themselves at key-on (as IES2 did). Click Connect, then turn the key. Untick it when the ignition is already on, to skip the wait. |
| **Compatibility mode (slow adapters)** | off | Slower timing for adapters or lines that lose bytes (as in IES2). Try it when you see *the adapter echoes a different byte* or frequent losses. Readings become slower. |
| **Raise DTR** | off | Sets the DTR line of the serial port. Some cables power their K-Line chip from it: try it if a cable never answers, not even with its echo. |
| **Enable active commands (tests, clear codes, adjustments)** | on | Unticked, the Run, Apply and Clear codes buttons only show a message. Each command asks for confirmation anyway. |
| **Reconnect automatically when communication is lost** | on | After a loss (ignition cycled, loose connector) the program retries by itself, after 0.5, 1 and 2 seconds and then every 5 seconds, and checks that the same ECU answers. Unticked, it stops at the first loss as IES2 did, and you reconnect with **Reconnect** (F12). |
| **Save a line trace of each connection (for bug reports)** | off | Writes every byte sent and received on the K-Line to a text file, `iaw_trace_<date>_<time>.txt` in the log folder. Its path is shown in the status bar. Attach it to any report about communication. |
| **Network bridge (RFC 2217), host:port** | empty | Address of a network bridge, for example `192.168.4.1:2217`. When it is valid, it appears at the top of the **Interface** list. See [Network bridge](#network-bridge). |
| **Folder for logs and line traces** | `Documents/marelli-iaw-ecu-studio/logs` | Where CSV recordings and line traces go; also where **Open log...** starts. Created when needed. |

## Network bridge

The cable can stay in the car, plugged into a Raspberry Pi (or any computer), while the program runs on a laptop on the same Wi-Fi or Ethernet network. On the computer with the cable:

```sh
iaw-cli bridge --port /dev/ttyUSB0 --listen 0.0.0.0:2217
```

Then write `<address of that computer>:2217` in **Network bridge** and choose it in **Interface**. ser2net and esp-link should work too. Use it on a private network only: whoever reaches the address reaches the car. Details: [Hardware](../../hardware.md#network-bridge-wi-fi-ethernet).

A plain Bluetooth or Wi-Fi serial adapter does not work: it cannot change the line speed, which these ECUs need.

## Files and folders

| What | Windows | Linux, Raspberry Pi, macOS |
| --- | --- | --- |
| Settings (`settings.json`) | `%APPDATA%\marelli-iaw-ecu-studio\` | `~/.config/marelli-iaw-ecu-studio/` |
| Extra or corrected ECU definitions | `%APPDATA%\marelli-iaw-ecu-studio\definitions\` | `~/.config/marelli-iaw-ecu-studio/definitions/` |
| CSV logs and line traces (default) | `Documents\marelli-iaw-ecu-studio\logs\` | `~/Documents/marelli-iaw-ecu-studio/logs/` |

To start again with the default settings, close the program and delete `settings.json`.

A definition file in the definitions folder with the same `id` as a built-in one replaces it: this is how a corrected or new ECU definition is tried without a new version of the program. See [ECU definition files](../../definitions.md).

The program also remembers the interface, the ECU chosen, whether the window was maximised and its size, the separate-scales choice and the graph window.
