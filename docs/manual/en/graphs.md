# Graphs

[← Manual](README.md) · Previous: [Actuator tests and adjustments](tests-and-adjustments.md) · Next: [Log replay →](log-replay.md)

The **Graphs** tab draws the chosen parameters over time while you are connected: useful to see how the idle settles, how the lambda correction swings, or what happens when a fault appears.

![Graphs](../../screenshots/graphs.png)

## Choosing what to plot

Tick the parameters in the **Graph** column of the [Parameters](parameters.md) tab. When you connect, the first four (engine speed, injection time, ignition advance, manifold pressure on most ECUs) are already ticked. A plotted parameter must also be ticked in **Read**, otherwise it gets no new points.

Each signal has its own colour and its name in the legend under the plot.

## Toolbar

| Control | What it does |
| --- | --- |
| **Separate scales** | Ticked: each signal has its own vertical axis, alternately on the left and on the right, so rpm (thousands) and lambda correction (around 1) are both readable. Unticked: one common axis, to compare signals with the same unit. |
| **Follow** | Ticked: the plot scrolls to show the latest values. Untick it to stop and look at a moment in the past while the data keeps coming. |
| **Window** | How many seconds are visible while following: 10, 30, 60 or 300 s, or **All** to see the whole session. |
| **Clear** | Erases the curves and starts again from now. |
| **Record CSV** | Starts or stops a [CSV recording](parameters.md#recording-a-csv-log), the same button as in the Parameters tab. |

## Mouse

- **Wheel**: zoom in and out.
- **Drag**: pan.
- **Double-click**: back to the automatic view.

## Good to know

- The plot keeps the last 36,000 points of each signal (about an hour and a half at 7 updates per second); older points are dropped. To keep a session, [record it](parameters.md#recording-a-csv-log).
- The graph starts again from zero at each new connection.
- Separate scales and the time window are remembered for the next start.
