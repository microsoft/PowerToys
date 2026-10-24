# Seamless Multi-Screen Cursor overlay snapshots

`SeamlessCursor.OverlaySnapshots.exe` draws the calibration overlay for made-up monitor layouts into PNG files. It uses
the real overlay code (`CalibrationOverlay.cpp`, through `RenderSnapshot` in [OverlaySnapshot.h](../OverlaySnapshot.h)),
but draws into bitmaps instead of windows, so you can check layouts you cannot set up on your desk: mixed orientation
and display scale, monitors on every side, gaps, a tiny primary display, a remote session.

It is a developer tool. It is built with the rest of PowerToys, into the tests folder, and is never packaged.

## Build and run

Build the project (this also builds the module, which generates the string resources the overlay loads):

```powershell
msbuild src\modules\MouseUtils\SeamlessCursor\OverlaySnapshots\SeamlessCursorOverlaySnapshots.vcxproj -p:Configuration=Debug -p:Platform=x64
```

Run it with an output folder (it defaults to `OverlaySnapshots` in the current folder):

```powershell
x64\Debug\tests\SeamlessCursor\SeamlessCursor.OverlaySnapshots.exe C:\Temp\Snapshots
```

For each layout it writes two files and prints the path of the first:

| File | Content |
|------|---------|
| `<layout>-overview.png` | Every monitor at its place in the virtual desktop, scaled down to about 3000 px wide, on a gray background, so gaps between monitors show |
| `<layout>-primary.png` | The primary monitor, where the panel is, at up to 1800 px wide, large enough to read the panel |

The layouts show the edge map, except the one with the pointer trail.

## Layouts

| Layout | What it checks |
|--------|----------------|
| `1-two-on-a-wide-monitor` | Saved calibrations, including a glued pair, are applied; the edge map shows where the pointer stops |
| `2-mixed-orientation-and-scale` | Portrait and landscape monitors at different scales; partial borders; glue offers across gaps; corner contact is ignored |
| `3-staircase` | Each monitor shares half a side with the next; a monitor touching only at a corner gets no border |
| `4-surround` | A monitor on every side of the primary: the panel and the switches make room |
| `5-tiny-primary` | A primary display too small for the full panel: the compact panel, and key hints wrapping without splitting a hint |
| `6-gaps` | Only gaps: glue offers, and none between monitors whose ranges do not overlap |
| `7-remote-session` | Windows does not report the monitors' device paths: the panel only says so and offers Close |
| `8-trail-crossing-and-wrap` | The pointer trail across a remapped crossing, and the wrap line after a CursorWrap wrap |

## Checking a change to the overlay

1. Before the change, run the tool into one folder, for example `C:\Temp\Before`.
2. Make the change, build, and run the tool into another folder, for example `C:\Temp\After`.
3. Compare the images side by side, or with any image diff tool. Text can differ by a pixel between machines, because
   of font rendering, so compare images made on the same machine.

The images are not compared automatically; nothing fails when they change.

## Adding a layout

Layouts are in `Layouts()` in [main.cpp](main.cpp). A layout lists its monitors in virtual-screen pixels, as Windows
display settings arranges them:

```cpp
layouts.push_back({ L"9-two-side-by-side",
                    { { L"Left", { -1920, 0, 0, 1080 }, 24.0, 1.0f },
                      { L"Right", { 0, 0, 2560, 1440 }, 27.0, 1.25f, true } } });
```

Each monitor has:

* a name, which the overlay shows and which also serves as its id in saved calibrations;
* its rectangle;
* its diagonal in inches, from which the physical pixel size is computed, as EDID would report it; the overlay uses it
  to predict where the lines go;
* its display scale (1.25 for 125 %), which sizes the overlay's text, switches and lines;
* whether it is the primary monitor, which gets the panel. Mark exactly one.

A layout can also have:

* `saved`: calibrations as if read from `calibration.json`. Without them, the lines are where the overlay predicts them;
* `identified = false`: as in a remote session, where Windows does not report the monitors' device paths;
* `trail = true` and `movePointer`: shows the pointer trail instead of the edge map, and moves the pointer.
  `movePointer` gets a `SnapshotPointer`, whose `Move(point, time)` moves the pointer as a mouse move over the overlay
  would, and whose `CurrentEngine()` gives the engine for the lines as placed, to remap a crossing the way Try while
  adjusting does, or to find where CursorWrap would land with `MapJump`. See `8-trail-crossing-and-wrap` for an example.
  Space the moves a few milliseconds apart; the snapshot is taken right after the last one, so older dots have faded.

## What it does not cover

The tool skips everything that needs real windows: creating them, mouse and keyboard input, the hint animation, and
saving. Check those in the overlay itself, by opening Calibrate in a PowerToys build.
