# Log replay

[← Manual](README.md) · Previous: [Graphs](graphs.md) · Next: [Options →](options.md)

The **Log replay** tab plays back a CSV log as if it were happening again, with the values and the plot moving in time. It works without a car and without connecting, so you can look at a drive at home, or at a log someone sent you.

![Log replay](../../screenshots/replay.png)

## Opening a log

Click **Open log...** and choose a file. The dialog opens in the log folder set in [Options](options.md#files-and-folders). Two kinds of file can be opened:

- logs recorded by this program (**Record CSV**, or `iaw-cli read --csv`);
- CSV logs exported by **IES2**, the original program: they are recognised by themselves and marked *IES2 export*.

Under the toolbar a summary line shows the ECU, the vehicle, the number of samples and the duration, for example *IAW-6F/16F · Punto 55 1.1 SPI 5M/6M ECE F2 · 4210 samples · 10:02.4*. If the file cannot be read, a message says why.

## Playing

| Control | What it does |
| --- | --- |
| **Play** / **Pause** | Starts and stops playback. At the end it stops by itself. |
| **Speed** | 1x (real time), 2x, 5x or 10x. |
| Position slider | Drag it to jump to any moment (scrubbing); the time is shown on its right. |
| **Separate scales** | One vertical axis per signal, as in [Graphs](graphs.md). |

On the left, the list of the columns in the log, each with its value at the current position and its unit. Tick a column to draw it in the plot on the right. In the plot, the wheel zooms, dragging pans and a double-click resets the view.

## Uses

- Look at a drive calmly after recording it on the road.
- Compare two sessions, for example before and after replacing a sensor.
- Read old IES2 logs on Linux or macOS.
- Attach a log to a [problem report](troubleshooting.md#reporting-a-problem) when a value looks wrong.

To replay the bytes of a whole session (not just the values), so that the program talks to the recording as if it were the car, use a line trace with `iaw-cli read --replay`: see [Command line](command-line.md#replaying-a-line-trace).
